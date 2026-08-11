namespace SharpSticks.Shared;

public struct DeferredDictionaryDisposable<TKey, T, TList> : IDisposable
	where T : IDisposable
	where TList : IReadOnlyDictionary<TKey, T>
{
	public TList List { get; }

	public DeferredDictionaryDisposable(TList value) => List = value;

	public bool DisposeIsSkipped { get; private set; }
	public bool DisposeValuesIsSkipped { get; private set; }

	public void SkipDispose() => DisposeIsSkipped = true;
	public void SkipDisposeValues() => DisposeValuesIsSkipped = true;

	public TList GetAndSkipDispose()
	{
		SkipDispose();
		return List;
	}

	public TList GetAndSkipDisposeValues()
	{
		SkipDisposeValues();
		return List;
	}

	public void Dispose()
	{
		if(DisposeIsSkipped)
		{
			return;
		}

		if (!DisposeValuesIsSkipped)
		{
			foreach (var disposable in List.Values)
			{
				disposable.Dispose();
			}
		}

		if (List is IDisposable d)
		{
			d.Dispose();
		}
	}
}