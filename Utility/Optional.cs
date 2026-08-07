public class Optional<T>
{
	public bool HasValue;
	public T Value;

	public Optional()
	{
	}

	public Optional(T value)
	{
		HasValue = true;
		Value = value;
	}
}