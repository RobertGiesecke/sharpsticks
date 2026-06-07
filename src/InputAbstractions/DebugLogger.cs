using System.Diagnostics;

namespace SharpSticks.InputAbstractions;

public sealed class DebugLogger
{
	private readonly Stopwatch _Stopwatch = Stopwatch.StartNew();
	private long _NextLogAtMs;

	public DebugLogger(int intervalMs)
	{
		if (intervalMs < 1)
		{
			throw new InvalidOperationException("Debug interval must be at least 1 ms.");
		}

		IntervalMs = intervalMs;
	}

	public int IntervalMs { get; }

	public bool ShouldLogNow()
	{
		var now = _Stopwatch.ElapsedMilliseconds;
		if (now < _NextLogAtMs)
		{
			return false;
		}

		_NextLogAtMs = now + IntervalMs;
		return true;
	}

	public void WriteLine(scoped NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler)
	{
		var line = Utils.FormatInterpolation($"[debug {DateTime.Now:HH:mm:ss.fff}] {interpolatedStringHandler}");

		// TextWriter has a span overload, so the composed line goes out
		// without ever materializing a string; Clear() hands the rented
		// buffer back to the pool.
		Console.Out.WriteLine(line.Text);
		line.Clear();
	}
}