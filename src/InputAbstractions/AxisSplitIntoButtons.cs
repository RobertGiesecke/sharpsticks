namespace SharpSticks.InputAbstractions;

public sealed record AxisSplitIntoButtons : ICombinedRoute, IConfigurableRoute
{
	public required AxisBinding Axis { get; init; }
	public required ImmutableArray<ButtonTarget> Outputs { get; init; }
	public AxisZoneOptions? Options { get; init; }

	IEnumerable<IRoute> ICombinedRoute.GetRoutes()
	{
		if (Outputs.IsDefaultOrEmpty)
		{
			throw new ArgumentException("At least one output button is required.", nameof(Outputs));
		}

		var o = Options ?? new();
		var (lo, hi) = Axis.Mode == AxisMode.Unsigned ? (0.0, 1.0) : (-1.0, 1.0);
		var step = (hi - lo) / Outputs.Length;
		var builder = ImmutableArray.CreateBuilder<IRoute>(Outputs.Length);
		for (var i = 0; i < Outputs.Length; i++)
		{
			var isLast = i == Outputs.Length - 1;

			// Every target kind is just an AxisZoneRoute now; the ButtonTarget decides the
			// sink (level for vJoy/key/mouse, pulse-on-entry for scroll).
			builder.Add(new AxisZoneRoute
			{
				Source = Axis,
				Target = Outputs[i],
				Min = lo + step * i,
				Max = isLast ? hi : lo + step * (i + 1),
				IncludeMax = isLast,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			});
		}

		return builder.MoveToImmutable();
	}
}

public readonly record struct AxisZone(double Min, double Max, ButtonTarget Output);

public readonly record struct AxisZoneOptions()
{
	public bool IncludeMax { get; init; } = true;
	public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;
	public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);
}