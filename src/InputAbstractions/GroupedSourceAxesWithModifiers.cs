namespace SharpSticks.InputAbstractions;

public sealed record GroupedSourceAxesWithModifiers
{
	public required ImmutableArray<AxisBindingWithModifier> SourceAxes { get; init; }
}