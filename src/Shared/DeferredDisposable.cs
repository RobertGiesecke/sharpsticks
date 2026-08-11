namespace SharpSticks.Shared;

public static class DeferredDisposable
{
	extension<T>(T value) where T : IDisposable
	{
		public DeferredDisposable<T> Defer() => new(value);
	}

	public static DeferredListDisposable<T, PooledList<T>> DeferList<T>(this PooledList<T> list)
		where T : IDisposable => new(list);

	public static DeferredListDisposable<T, IReadOnlyCollection<T>> DeferList<T>(this IReadOnlyCollection<T> list)
		where T : IDisposable => new(list);

	public static DeferredDictionaryDisposable<TKey, T, PooledDictionary<TKey, T>>
		DeferDictionary<TKey, T>(this PooledDictionary<TKey, T> list) where T : IDisposable => new(list);
}

public struct DeferredDisposable<T> : IDisposable
	where T : IDisposable
{
	public T Value { get; }

	public DeferredDisposable(T value) => Value = value;

	public bool DisposeIsSkipped { get; private set; }

	public void SkipDispose() => DisposeIsSkipped = true;

	public T GetAndSkipDispose()
	{
		SkipDispose();
		return Value;
	}

	public void Dispose()
	{
		if(DisposeIsSkipped)
		{
			return;
		}

		Value.Dispose();
	}
}