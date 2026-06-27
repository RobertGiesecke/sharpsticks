using System.Diagnostics;

namespace SharpSticks.Shared;

/// <summary>
/// Monotonic time abstraction the macro engine uses to schedule waits and
/// decide when a deferred step is due. Production code uses
/// <see cref="StopwatchTimeSource"/>; tests inject a fake to advance virtual
/// time without sleeping.
/// </summary>
public interface ITimeSource
{
	long GetTimestamp();
	long Frequency { get; }
}

public sealed class StopwatchTimeSource : ITimeSource
{
	public static StopwatchTimeSource Instance { get; } = new();

	public long GetTimestamp() => Stopwatch.GetTimestamp();
	public long Frequency => Stopwatch.Frequency;
}
