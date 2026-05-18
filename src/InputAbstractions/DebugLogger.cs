using System.Diagnostics;
using System.Text;

namespace SharpSticks.InputAbstractions;

public sealed class DebugLogger
{
	private readonly Stopwatch _Stopwatch = Stopwatch.StartNew();
	private long _NextLogAtMs;
	PooledQueue<StringBuilder> _StringBuilderPool = new();

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

	public void WriteLine(string message)
	{
		Console.Error.WriteLine($"[debug {DateTime.Now:HH:mm:ss.fff}] {message}");
	}

	public void WriteBlock(StringBuilder builder)
	{
		if (builder.Length == 0)
		{
			return;
		}

		var lines = builder.ToString().TrimEnd().Split(Environment.NewLine);
		foreach (var line in lines)
		{
			WriteLine(line);
		}
	}
}