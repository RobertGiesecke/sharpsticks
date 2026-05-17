namespace ScaledAxisCSharp.InputAbstractions;

public readonly record struct RouteAxisOptions()
{
	public IAxisModifier? Modifier { get; init; }
	public double Scale { get; init; } = 1.0;
	public double Offset { get; init; }
}