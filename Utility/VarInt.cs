using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Protocol.Utility.IO;

public static class VarInt
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint EncodeZigZag32(int n)
	{
		return (uint)((n << 1) ^ (n >> 31));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int DecodeZigZag32(uint n)
	{
		return (int)(n >> 1) ^ -(int)(n & 1);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong EncodeZigZag64(long n)
	{
		return (ulong)((n << 1) ^ (n >> 63));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static long DecodeZigZag64(ulong n)
	{
		return (long)(n >> 1) ^ -(long)(n & 1);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint ReadRawVarInt32(Stream buf, int maxSize)
	{
		uint result = 0;
		var j = 0;
		int b0;

		do
		{
			b0 = buf.ReadByte();
			if (b0 < 0) throw new EndOfStreamException("Not enough bytes for VarInt");

			result |= (uint)(b0 & 0x7f) << (j++ * 7);

			if (j > maxSize) throw new OverflowException("VarInt too big");
		} while ((b0 & 0x80) == 0x80);

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong ReadRawVarInt64(Stream buf, int maxSize, bool printBytes = false)
	{
		var bytes = new List<byte>();

		ulong result = 0;
		var j = 0;
		int b0;

		do
		{
			b0 = buf.ReadByte();
			bytes.Add((byte)b0);
			if (b0 < 0) throw new EndOfStreamException("Not enough bytes for VarInt");

			result |= (ulong)(b0 & 0x7f) << (j++ * 7);

			if (j > maxSize) throw new OverflowException("VarInt too big");
		} while ((b0 & 0x80) == 0x80);

		var byteArray = bytes.ToArray();

	//	if (printBytes) Console.WriteLine($"Long bytes: {Protocol.Codec.IO.Packet.HexDump(byteArray)} ");

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteRawVarInt32(Stream buf, uint value)
	{
		while ((value & -128) != 0)
		{
			buf.WriteByte((byte)((value & 0x7F) | 0x80));
			value >>= 7;
		}

		buf.WriteByte((byte)value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteRawVarInt64(Stream buf, ulong value)
	{
		while ((value & 0xFFFFFFFFFFFFFF80) != 0)
		{
			buf.WriteByte((byte)((value & 0x7F) | 0x80));
			value >>= 7;
		}

		buf.WriteByte((byte)value);
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteInt32(Stream stream, int value)
	{
		WriteRawVarInt32(stream, (uint)value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ReadInt32(Stream stream)
	{
		return (int)ReadRawVarInt32(stream, 5);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteSInt32(Stream stream, int value)
	{
		WriteRawVarInt32(stream, EncodeZigZag32(value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ReadSInt32(Stream stream)
	{
		return DecodeZigZag32(ReadRawVarInt32(stream, 5));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteUInt32(Stream stream, uint value)
	{
		WriteRawVarInt32(stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint ReadUInt32(Stream stream)
	{
		return ReadRawVarInt32(stream, 5);
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteInt64(Stream stream, long value)
	{
		WriteRawVarInt64(stream, (ulong)value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long ReadInt64(Stream stream, bool printBytes = false)
	{
		return (long)ReadRawVarInt64(stream, 10, printBytes);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteSInt64(Stream stream, long value)
	{
		WriteRawVarInt64(stream, EncodeZigZag64(value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long ReadSInt64(Stream stream)
	{
		return DecodeZigZag64(ReadRawVarInt64(stream, 10));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void WriteUInt64(Stream stream, ulong value)
	{
		WriteRawVarInt64(stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong ReadUInt64(Stream stream)
	{
		return ReadRawVarInt64(stream, 10);
	}
}