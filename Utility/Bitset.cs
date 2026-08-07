using System.Numerics;

namespace Protocol.Utility;

public class Bitset
{
	public Bitset(int size)
	{
		Size = size;
		IntValue = BigInteger.Zero;
	}


	public Bitset(int size, BigInteger intValue)
	{
		Size = size;
		IntValue = intValue;
	}

	public int Size { get; }
	public BigInteger IntValue { get; private set; }


	public ulong Low
	{
		get
		{
			if (IntValue <= ulong.MaxValue)
				return (ulong)IntValue;

			return (ulong)(IntValue & ulong.MaxValue);
		}
	}


	public ulong High
	{
		get
		{
			if (Size <= 64)
				return 0;

			return (ulong)(IntValue >> 64);
		}
	}


	public void Set(int i)
	{
		if (i >= Size)
			throw new IndexOutOfRangeException("index out of bounds");

		IntValue |= BigInteger.One << i;
	}


	public void Unset(int i)
	{
		if (i >= Size)
			throw new IndexOutOfRangeException("index out of bounds");

		IntValue &= ~(BigInteger.One << i);
	}


	public bool Load(int i)
	{
		if (i >= Size)
			throw new IndexOutOfRangeException("index out of bounds");

		return (IntValue & (BigInteger.One << i)) != BigInteger.Zero;
	}


	public int Len()
	{
		return Size;
	}


	public bool HasFlag(ulong flags)
	{
		return (IntValue & flags) != 0;
	}
}