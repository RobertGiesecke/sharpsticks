namespace SharpSticks.Shared;

public struct DeferredListDisposable<T, TList> : IDisposable
	where T : IDisposable
	where TList : IReadOnlyCollection<T>
{
	public TList List { get; }

	public DeferredListDisposable(TList value) => List = value;

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
			if (List is IReadOnlyList<T> l)
			{
				for (var i = 0; i < l.Count; i++)
				{
					l[i].Dispose();
				}
			}
			else
			{
				foreach (var disposable in List)
				{
					disposable.Dispose();
				}
			}
		}

		if (List is IDisposable d)
		{
			d.Dispose();
		}
	}
}