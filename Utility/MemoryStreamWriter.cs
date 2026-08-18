using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Protocol.Utility.IO;

public class MemoryStreamWriter
{
	private readonly Stream _stream;

	public MemoryStreamWriter(Stream stream)
	{
		_stream = stream;
	}

	public Stream InnerStream => _stream;

	public void WriteByte(byte value)
	{
		_stream.WriteByte(value);
	}
	public void WriteBool(bool value)
	{
		if (value)
		{
			WriteByte(1);
		}
		else
		{
			WriteByte(0);
		}
	}
	public void Write(byte[] buffer)
	{
		_stream.Write(buffer, 0, buffer.Length);
	}

	public void Write(ReadOnlyMemory<byte> buffer)
	{
		_stream.Write(buffer.Span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteInt16(short value)
	{
		Span<byte> span = stackalloc byte[2];
		BinaryPrimitives.WriteInt16LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteUInt16(ushort value)
	{
		Span<byte> span = stackalloc byte[2];
		BinaryPrimitives.WriteUInt16LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteInt32(int value)
	{
		Span<byte> span = stackalloc byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteUInt32(uint value)
	{
		Span<byte> span = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteInt64(long value)
	{
		Span<byte> span = stackalloc byte[8];
		BinaryPrimitives.WriteInt64LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteUInt64(ulong value)
	{
		Span<byte> span = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteSingle(float value)
	{
		Span<byte> span = stackalloc byte[4];
		BinaryPrimitives.WriteSingleLittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteDouble(double value)
	{
		Span<byte> span = stackalloc byte[8];
		BinaryPrimitives.WriteDoubleLittleEndian(span, value);
		_stream.Write(span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteVarInt32(int value)
	{
		VarInt.WriteInt32(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteVarUInt32(uint value)
	{
		VarInt.WriteUInt32(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteVarInt64(long value)
	{
		VarInt.WriteInt64(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteSInt32(int value)
	{
		VarInt.WriteSInt32(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteSInt64(long value)
	{
		VarInt.WriteSInt64(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteVarUInt64(ulong value)
	{
		VarInt.WriteUInt64(_stream, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLengthPrefixedString(string value)
	{
		WriteLengthPrefixedBytes(Encoding.UTF8.GetBytes(value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLengthPrefixedBytes(byte[] buffer)
	{
		VarInt.WriteUInt64(_stream, (ulong)buffer.Length);
		_stream.Write(buffer, 0, buffer.Length);
	}
}
