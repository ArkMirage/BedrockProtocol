// Copyright © 2026 SculkCatalystMC. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of the MPL was not
// distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// SPDX-License-Identifier: MPL-2.0
//
// C# port of:
//   src/sculk/protocol/connection/compression/Snappy.cpp
// Self-contained C# implementation — no external Snappy library required.
//
// The C++ source returns std::expected<T, ErrorInfo> from the decompress API;
// this port follows the idiomatic .NET convention of throwing InvalidDataException
// on malformed input instead.
//
// Target: .NET 6+ (Span<T>, MemoryStream).

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Protocol.Connection.Compression;

/// <summary>
/// Google Snappy compression format (https://github.com/google/snappy).
/// Self-contained C# implementation — no external Snappy library required.
/// Compatible with bytes produced/consumed by the native libsnappy used by
/// the C++ <c>compression::snappy</c> module.
/// </summary>
public static class Snappy
{
	// --- public API mirrors C++ compression::snappy ---

	/// <summary>
	/// Compresses <paramref name="data"/>. Returns an empty buffer when the
	/// input is empty, matching the C++ wrapper's short-circuit.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        using var output = new MemoryStream(MaxCompressedLength(data.Length));
        WriteVarint32(output, (uint)data.Length);

        int n = data.Length;
        if (n < 4)
        {
            // Not enough bytes to hash; emit the whole thing as one literal.
            EmitLiteral(output, data);
            return output.ToArray();
        }

        // 14-bit hash table (1 << 14 entries). Matches snappy's MAX_HASH_TABLE_BITS = 14.
        const int HashBits = 14;
        const int HashShift = 32 - HashBits;
        const uint HashMul = 0x1e35a7bdu;
        var table = new int[1 << HashBits];
        Array.Fill(table, -1);

        int ip = 0;
        int nextEmit = 0;
        int ipLimit = n - 4; // need 4 bytes to hash

        while (ip < ipLimit)
        {
            uint p = (uint)data[ip]
                     | ((uint)data[ip + 1] << 8)
                     | ((uint)data[ip + 2] << 16)
                     | ((uint)data[ip + 3] << 24);
            uint hash = (p * HashMul) >> HashShift;
            int candidate = table[hash];
            table[hash] = ip;

            // Reject stale / future / out-of-reach candidates.
            if (candidate < 0
                || candidate >= ip
                || data[candidate] != data[ip]
                || data[candidate + 1] != data[ip + 1]
                || data[candidate + 2] != data[ip + 2]
                || data[candidate + 3] != data[ip + 3])
            {
                ip++;
                continue;
            }

            int offset = ip - candidate;
            int matched = 4;
            // Extend the match forward as far as possible.
            while (ip + matched < n && data[candidate + matched] == data[ip + matched])
            {
                matched++;
            }

            // Emit any pending literal bytes between nextEmit and ip.
            if (nextEmit < ip)
            {
                EmitLiteral(output, data.Slice(nextEmit, ip - nextEmit));
            }

            EmitCopy(output, offset, matched);

            ip += matched;
            nextEmit = ip;
        }

        // Emit trailing literal (covers the last <4 bytes too).
        if (nextEmit < n)
        {
            EmitLiteral(output, data.Slice(nextEmit));
        }

        return output.ToArray();
    }

	/// <summary>
	/// Decompresses a Snappy stream. Enforces a 64 MiB output cap and full
	/// structural validation; throws <see cref="InvalidDataException"/> on any
	/// malformed input.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] Decompress(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new InvalidDataException("snappy decompress failed: input is empty");
        }

        if (!TryReadVarint32(data, out uint uncompressedLengthU32, out int headerBytes))
        {
            throw new InvalidDataException("snappy decompress failed: invalid compressed payload");
        }

        const ulong kMaxOutputSize = 64uL * 1024uL * 1024uL;
        if (uncompressedLengthU32 > kMaxOutputSize)
        {
            throw new InvalidDataException("snappy decompress failed: output exceeds configured limit");
        }

        if (uncompressedLengthU32 == 0)
        {
            return Array.Empty<byte>();
        }

        int uncompressedLength = (int)uncompressedLengthU32;
        byte[] output = new byte[uncompressedLength];
        int ip = headerBytes;
        int op = 0;
        int n = data.Length;

        while (ip < n)
        {
            byte tag = data[ip++];
            int type = tag & 0x03;

            switch (type)
            {
                case 0: // Literal
                {
                    int lenCode = (tag >> 2) & 0x3F;
                    uint literalLengthU32;
                    if (lenCode < 60)
                    {
                        literalLengthU32 = (uint)lenCode + 1u;
                    }
                    else
                    {
                        int extraBytes = lenCode - 59; // 1..4
                        if (ip + extraBytes > n)
                        {
                            throw new InvalidDataException("snappy decompress failed: truncated literal length");
                        }

                        uint len = 0;
                        for (int i = 0; i < extraBytes; i++)
                        {
                            len |= (uint)data[ip++] << (8 * i);
                        }

                        literalLengthU32 = len + 1u;
                    }

                    if (literalLengthU32 > (uint)(uncompressedLength - op))
                    {
                        throw new InvalidDataException("snappy decompress failed: output overflow");
                    }

                    int literalLength = (int)literalLengthU32;
                    if (ip + literalLength > n)
                    {
                        throw new InvalidDataException("snappy decompress failed: truncated literal data");
                    }

                    data.Slice(ip, literalLength).CopyTo(output.AsSpan(op));
                    ip += literalLength;
                    op += literalLength;
                    break;
                }

                case 1: // Copy with 1-byte offset (11-bit offset, 3-bit length)
                {
                    int length = 4 + ((tag >> 2) & 0x07);
                    if (ip >= n)
                    {
                        throw new InvalidDataException("snappy decompress failed: truncated copy offset");
                    }

                    uint offsetU32 = (((uint)tag >> 5) & 0x07u) << 8 | data[ip++];
                    if (!TryApplyCopy(output, ref op, offsetU32, length, uncompressedLength))
                    {
                        throw new InvalidDataException("snappy decompress failed: invalid copy offset");
                    }

                    break;
                }

                case 2: // Copy with 2-byte offset (16-bit offset, 6-bit length)
                {
                    int length = 4 + ((tag >> 2) & 0x3F);
                    if (ip + 2 > n)
                    {
                        throw new InvalidDataException("snappy decompress failed: truncated copy offset");
                    }

                    uint offsetU32 = (uint)data[ip] | ((uint)data[ip + 1] << 8);
                    ip += 2;
                    if (!TryApplyCopy(output, ref op, offsetU32, length, uncompressedLength))
                    {
                        throw new InvalidDataException("snappy decompress failed: invalid copy offset");
                    }

                    break;
                }

                case 3: // Copy with 4-byte offset (32-bit offset, 6-bit length)
                {
                    int length = 4 + ((tag >> 2) & 0x3F);
                    if (ip + 4 > n)
                    {
                        throw new InvalidDataException("snappy decompress failed: truncated copy offset");
                    }

                    uint offsetU32 = (uint)data[ip]
                                     | ((uint)data[ip + 1] << 8)
                                     | ((uint)data[ip + 2] << 16)
                                     | ((uint)data[ip + 3] << 24);
                    ip += 4;
                    if (!TryApplyCopy(output, ref op, offsetU32, length, uncompressedLength))
                    {
                        throw new InvalidDataException("snappy decompress failed: invalid copy offset");
                    }

                    break;
                }
            }
        }

        if (op != uncompressedLength)
        {
            throw new InvalidDataException("snappy decompress failed");
        }

        return output;
    }

    // --- Snappy internals ---

    /// <summary>
    /// Matches libsnappy's MaxCompressedLength: 32 + sourceLength + sourceLength/6.
    /// Upper bound on the compressed size; used to pre-size the output buffer.
    /// </summary>
    private static int MaxCompressedLength(int sourceLength)
    {
        return 32 + sourceLength + sourceLength / 6;
    }

    private static void EmitLiteral(MemoryStream output, ReadOnlySpan<byte> literal)
    {
        int n = literal.Length;
        Debug.Assert(n > 0, "Empty literals must never be emitted.");

        if (n <= 60)
        {
            output.WriteByte((byte)((n - 1) << 2));
        }
        else if (n <= 256)
        {
            output.WriteByte((byte)(60 << 2));
            output.WriteByte((byte)(n - 1));
        }
        else if (n <= 65536)
        {
            output.WriteByte((byte)(61 << 2));
            int v = n - 1;
            output.WriteByte((byte)(v & 0xFF));
            output.WriteByte((byte)((v >> 8) & 0xFF));
        }
        else if (n <= 16777216)
        {
            output.WriteByte((byte)(62 << 2));
            int v = n - 1;
            output.WriteByte((byte)(v & 0xFF));
            output.WriteByte((byte)((v >> 8) & 0xFF));
            output.WriteByte((byte)((v >> 16) & 0xFF));
        }
        else
        {
            output.WriteByte((byte)(63 << 2));
            uint v = (uint)(n - 1);
            output.WriteByte((byte)(v & 0xFF));
            output.WriteByte((byte)((v >> 8) & 0xFF));
            output.WriteByte((byte)((v >> 16) & 0xFF));
            output.WriteByte((byte)((v >> 24) & 0xFF));
        }

        output.Write(literal);
    }

    /// <summary>
    /// Splits a long copy into chunks whose length-4 fits in 6 bits (max single-copy length = 67).
    /// </summary>
    private static void EmitCopy(MemoryStream output, int offset, int length)
    {
        // Snappy's split strategy: chunk into 64-byte pieces, with a 60-byte tail
        // adjustment when the remainder falls in (64, 67].
        while (length >= 68)
        {
            EmitCopyExact(output, offset, 64);
            length -= 64;
        }

        if (length > 64)
        {
            EmitCopyExact(output, offset, 60);
            length -= 60;
        }

        EmitCopyExact(output, offset, length);
    }

    private static void EmitCopyExact(MemoryStream output, int offset, int length)
    {
        Debug.Assert(length >= 4 && length <= 67, "Copy length out of encodable range.");

        if (length <= 11 && offset <= 2047)
        {
            // type=01 | (length-4) bits 2-4 | (offset>>8) bits 5-7 | low 8 bits in next byte
            uint tag = 0x01u
                       | (((uint)(length - 4) & 0x07u) << 2)
                       | (((uint)(offset >> 8) & 0x07u) << 5);
            output.WriteByte((byte)tag);
            output.WriteByte((byte)(offset & 0xFF));
        }
        else if (offset <= 65535)
        {
            // type=02 | (length-4) bits 2-7 | 16-bit offset LE in next 2 bytes
            uint tag = 0x02u | (((uint)(length - 4) & 0x3Fu) << 2);
            output.WriteByte((byte)tag);
            output.WriteByte((byte)(offset & 0xFF));
            output.WriteByte((byte)((offset >> 8) & 0xFF));
        }
        else
        {
            // type=03 | (length-4) bits 2-7 | 32-bit offset LE in next 4 bytes
            uint tag = 0x03u | (((uint)(length - 4) & 0x3Fu) << 2);
            output.WriteByte((byte)tag);
            output.WriteByte((byte)(offset & 0xFF));
            output.WriteByte((byte)((offset >> 8) & 0xFF));
            output.WriteByte((byte)((offset >> 16) & 0xFF));
            output.WriteByte((byte)((offset >> 24) & 0xFF));
        }
    }

    /// <summary>
    /// Applies a back-reference copy of <paramref name="length"/> bytes from
    /// <paramref name="offsetU32"/> bytes before the current write position.
    /// Handles overlapping copies (offset &lt; length) correctly via byte-by-byte copy.
    /// </summary>
    private static bool TryApplyCopy(byte[] output, ref int op, uint offsetU32, int length, int outputLength)
    {
        if (offsetU32 == 0 || offsetU32 > (uint)op)
        {
            return false;
        }

        if (length > outputLength - op)
        {
            return false;
        }

        int offset = (int)offsetU32;

        if (offset >= length)
        {
            // Non-overlapping — fast path.
            new Span<byte>(output, op - offset, length).CopyTo(output.AsSpan(op));
            op += length;
        }
        else
        {
            // Overlapping — must copy byte-by-byte so freshly written bytes
            // participate in the copy.
            for (int i = 0; i < length; i++)
            {
                output[op] = output[op - offset];
                op++;
            }
        }

        return true;
    }

    private static void WriteVarint32(MemoryStream output, uint value)
    {
        while (value > 0x7Fu)
        {
            output.WriteByte((byte)((value & 0x7Fu) | 0x80u));
            value >>= 7;
        }

        output.WriteByte((byte)value);
    }

    private static bool TryReadVarint32(ReadOnlySpan<byte> data, out uint value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;
        int shift = 0;
        while (bytesRead < data.Length)
        {
            byte b = data[bytesRead++];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
            if (shift >= 32)
            {
                return false; // varint too long for uint32
            }
        }

        return false; // ran out of input before terminator
    }
}
