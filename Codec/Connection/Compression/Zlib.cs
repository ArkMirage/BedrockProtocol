using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Protocol.Connection.Compression;

/// <summary>
/// Raw DEFLATE (zlib windowBits = -15) compression. Byte-for-byte interoperable
/// with the C++ <c>compression::zlib</c> module: both emit raw DEFLATE streams
/// with no zlib/gzip wrapper.
/// </summary>
public static class Zlib
{
    private const int StreamChunk = 65536;

	/// <summary>
	/// Compresses <paramref name="input"/> using raw DEFLATE at the default
	/// compression level. On failure the input is returned unchanged, matching
	/// the C++ implementation's fallback behavior.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] Compress(ReadOnlySpan<byte> input)
    {
        // .NET's DeflateStream emits 0 bytes for empty input (a non-standard
        // optimization), whereas zlib/C++ emits the canonical 2-byte empty
        // final fixed-Huffman block (0x03 0x00). Emit it manually so the output
        // stays byte-interoperable with the C++ zlib wrapper.
        if (input.IsEmpty)
        {
            return new byte[] { 0x03, 0x00 };
        }

        try
        {
            using var output = new MemoryStream(input.Length);
            // DeflateStream writes raw DEFLATE (RFC 1951) with no zlib header,
            // matching deflateInit2(windowBits = -15, memLevel = 8, Z_DEFAULT_STRATEGY).
            // CompressionLevel.Optimal maps to zlib's Z_DEFAULT_COMPRESSION (level 6).
            using (var ds = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                ds.Write(input);
            }

            return output.ToArray();
        }
        catch
        {
            // Mirror C++: on any failure, return the input bytes verbatim.
            return input.ToArray();
        }
    }

	/// <summary>
	/// Decompresses a raw DEFLATE stream. Throws <see cref="InvalidDataException"/>
	/// on truncated or invalid input.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] Decompress(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
        {
            throw new InvalidDataException("Failed to decompress data");
        }

        // DeflateStream requires a seekable stream; copy the span into one.
        using var inputMs = new MemoryStream(input.ToArray());
        using var ds = new DeflateStream(inputMs, CompressionMode.Decompress);
        using var outputMs = new MemoryStream(input.Length * 2);

        // Let DeflateStream's own exceptions propagate as InvalidDataException.
        ds.CopyTo(outputMs, StreamChunk);

        return outputMs.ToArray();
    }
}
