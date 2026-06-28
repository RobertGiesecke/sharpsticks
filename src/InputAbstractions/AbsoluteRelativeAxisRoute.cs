namespace SharpSticks.InputAbstractions;

public sealed record AbsoluteRelativeAxisRoute : ICombinedRoute
{
	public required AxisBinding Binding { get; init; }
	public required AbsoluteRelativeAxisOptions Options { get; init; }

	public IEnumerable<IRoute> GetRoutes()
	{
		if (Options is { IncreaseAxis.OutputDeviceId: < 1 } or { DecreaseAxis.OutputDeviceId: < 1 })
		{
			throw new InvalidOperationException("Output device ids are 1-based.");
		}

		// Same axis for both directions = bidirectional mode: one route whose
		// modifier rests at center, pulsing positive on increase and negative
		// on decrease. The rest-position options are not used in this mode.
		if (Options.IncreaseAxis == Options.DecreaseAxis)
		{
			return
			[
				Binding.RouteTo(
					Options.IncreaseAxis,
					modifier: AbsoluteRelativeAxisModifier.CreateBidirectional(Options)),
			];
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