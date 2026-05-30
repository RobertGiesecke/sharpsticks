namespace SharpSticks.InputAbstractions;

public sealed record GroupedSourceAxes
{
	public required ImmutableArray<AxisBinding> SourceAxes { get; init; }
}