namespace SharpSticks.InputAbstractions;

public sealed record AbsoluteRelativeAxisRoute : ICombinedRoute
{
	public required AxisBinding Binding { get; init; }
	public required AbsoluteRelativeAxisOptions Options { get; init; }

	public IEnumerable<IBoundRoute> GetRoutes()
	{
		if (Options is { IncreaseAxis.OutputDeviceId: < 1 } or { DecreaseAxis.OutputDeviceId: < 1 })
		{
			throw new InvalidOperationException("Output device ids are 1-based.");
		}

		if (Options.IncreaseAxis == Options.DecreaseAxis)
		{
			throw new InvalidOperationException("IncreaseAxis and DecreaseAxis must be different.");
		}

		var (increaseModifier, decreaseModifier) = AbsoluteRelativeAxisModifier.Create(Options);

		return
		[
			Binding.RouteTo(
				Options.IncreaseAxis,
				modifier: increaseModifier),
			Binding.RouteTo(
				Options.DecreaseAxis,
				modifier: decreaseModifier),
		];
	}
}