namespace SharpSticks.InputAbstractions;

public sealed record AxesWithZones : ICombinedRoute, IConfigurableRoute
{
	public required GroupedSourceAxes GroupedSourceAxes { get; init; }
	public required ImmutableArray<AxisZone> Zones { get; init; }
	public AxisZoneOptions? Options { get; init; }

	IEnumerable<IRoute> ICombinedRoute.GetRoutes()
	{
		var o = Options ?? new();
		foreach (var zone in Zones)
		{
			yield return new MultiAxesToButtonRoute
			{
				Sources = GroupedSourceAxes.SourceAxes,
				Target = zone.Output,
				Min = zone.Min,
				Max = zone.Max,
				IncludeMax = o.IncludeMax,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			};
		}
	}
}