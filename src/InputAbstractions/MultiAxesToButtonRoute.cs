namespace SharpSticks.InputAbstractions;

public sealed record MultiAxesToButtonRoute : ICombinedRoute
{
	public required ImmutableArray<AxisBinding> Sources { get; init; }

	public required OutputButtonBinding OutputBinding { get; init; }
	public required double Min { get; init; }
	public required double Max { get; init; }
	/// <summary>
	/// If <c>true</c> the zone is <c>[Min, Max]</c>; otherwise <c>[Min, Max)</c>.
	/// Defaults to <c>true</c> — single-range API uses closed bounds, while
	/// the even-split helper sets this to <c>false</c> on all but the last zone.
	/// </summary>
	public bool IncludeMax { get; init; } = true;

	public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;

	/// <summary>Only used when <see cref="Mode"/> is <see cref="AxisZoneTriggerMode.Pulse"/>.</summary>
	public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);

	public IEnumerable<IBoundRoute> GetRoutes()
	{
		return Sources.Select(s => new AxisToButtonRoute
		{
			Source = s,
			OutputBinding = OutputBinding,
			Min = Min,
			Max = Max,
			IncludeMax = IncludeMax,
			Mode = Mode,
			PulseDuration = PulseDuration
		});
	}
}