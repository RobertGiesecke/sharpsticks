namespace SharpSticks.InputAbstractions;

public enum MergeMode
{
	Sum,
	Average,
	Min,
	Max,
	Multiply,
}

public readonly record struct MergeAxesOptions()
{
	public required OutputAxisBinding OutputBinding { get; init; }
	public RouteAxisOptions? First { get; init; }
	public RouteAxisOptions? Second { get; init; }
	public MergeMode Mode { get; init; } = MergeMode.Sum;
}
