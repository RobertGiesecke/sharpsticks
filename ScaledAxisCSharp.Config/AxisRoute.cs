namespace ScaledAxisCSharp.Config;

public sealed record AxisRoute
{
	public required AxisBinding Source { get; init; }
	public required VJoyAxis TargetAxis { get; init; }
	public double Scale { get; init; }
	public double Offset { get; init; }
	public required IAxisModifier? Modifier { get; init; }
}