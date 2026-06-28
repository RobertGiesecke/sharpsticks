namespace SharpSticks.OutputAbstractions;

public sealed record RouteToSameAxesOnOutputCombinedRoute : ICombinedRoute, IConfigurableRoute
{
	public required ImmutableArray<AxisBinding> SourceAxes { get; init; }
	public required uint OutputDeviceId { get; init; }
	public RouteAxisOptions? Options { get; init; }

	public IEnumerable<IRoute> GetRoutes() =>
		SourceAxes.Distinct()
			.Select(a => a.RouteToSameAxisOnOutput(OutputDeviceId, Options));
}