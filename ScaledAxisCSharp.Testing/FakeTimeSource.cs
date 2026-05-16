namespace ScaledAxisCSharp.Testing;

/// <summary>
/// Deterministic <see cref="ITimeSource"/> for tests. Defaults to a tick
/// frequency of 10,000,000 (1 tick = 100 ns, matching <see cref="TimeSpan.TicksPerSecond"/>).
/// Tests advance virtual time with <see cref="Advance"/> instead of sleeping.
/// </summary>
public sealed class FakeTimeSource : ITimeSource
{
	public long Frequency { get; init; } = TimeSpan.TicksPerSecond;
	private long _Now;

	public long GetTimestamp() => _Now;

	public void Advance(TimeSpan duration)
	{
		_Now += (long)(duration.TotalSeconds * Frequency);
	}
}
