// ==================== Optional<T> ====================
using Protocol.Utility.IO;

public class Optional<T>
{
	public bool HasValue;
	public T Value;

	public Optional() { }

	public Optional(T value)
	{
		HasValue = true;
		Value = value;
	}
	public void Read(MemoryStreamReader reader, Func<T> actionFunc)
	{
		HasValue = reader.ReadBool();
		if (HasValue)
		{
			Value = actionFunc();
		}
		else
		{
			Value = default!;
		}
	}
	public void Write(MemoryStreamWriter writer, Action<T> onWritten)
	{
		writer.WriteBool(HasValue);
		if (HasValue)
		{
			onWritten?.Invoke(Value);
		}
	}

}

// ==================== DoubleOptional<T> ====================
public class DoubleOptional<T>
{
	public bool HasOuter { get; private set; }
	public bool HasInner { get; private set; }
	public T Value { get; private set; }

	public DoubleOptional()
	{
		HasOuter = false;
		HasInner = false;
		Value = default!;
	}

	public DoubleOptional(bool outerExists)
	{
		HasOuter = true;
		HasInner = false;
		Value = default!;
	}

	public DoubleOptional(T value)
	{
		HasOuter = true;
		HasInner = true;
		Value = value;
	}

	public static DoubleOptional<T> FromOptional(Optional<T> inner)
	{
		if (inner == null) return new DoubleOptional<T>();
		if (!inner.HasValue) return new DoubleOptional<T>(true);
		return new DoubleOptional<T>(inner.Value);
	}

	public void Read(MemoryStreamReader reader, Func<T> valueReader)
	{
		bool outer = reader.ReadBool();
		if (!outer)
		{
			HasOuter = false;
			HasInner = false;
			Value = default!;
			return;
		}

		HasOuter = true;
		var inner = new Optional<T>();
		inner.Read(reader, valueReader);

		if (inner.HasValue)
		{
			HasInner = true;
			Value = inner.Value;
		}
		else
		{
			HasInner = false;
			Value = default!;
		}
	}

	public void Write(MemoryStreamWriter writer, Action<T> valueWriter)
	{
		writer.WriteBool(HasOuter);
		if (!HasOuter)
			return;

		var inner = new Optional<T>();
		if (HasInner)
			inner = new Optional<T>(Value);
		inner.Write(writer, valueWriter);
	}
}