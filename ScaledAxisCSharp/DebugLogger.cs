using System.Diagnostics;
using System.Text;

namespace ScaledAxisCSharp;

internal sealed class DebugLogger
{
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
	private readonly int _intervalMs;
	private long _nextLogAtMs;

	public DebugLogger(int intervalMs)
	{
		if (intervalMs < 1)
		{
			throw new InvalidOperationException("Debug interval must be at least 1 ms.");
		}

		_intervalMs = intervalMs;
	}

	public int IntervalMs => _intervalMs;

	public bool ShouldLogNow()
	{
		var now = _stopwatch.ElapsedMilliseconds;
		if (now < _nextLogAtMs)
		{
			return false;
		}

		_nextLogAtMs = now + _intervalMs;
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

		var lines = builder.ToString().TrimEnd().Split(Environment.NewLine, StringSplitOptions.None);
		foreach (var line in lines)
		{
			WriteLine(line);
		}
	}
}
