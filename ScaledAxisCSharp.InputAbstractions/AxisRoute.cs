namespace ScaledAxisCSharp.InputAbstractions;

public sealed record AxisRoute
{
	public required AxisBinding Source { get; init; }
	public required uint OutputDeviceId { get; init; }
	public required PhysicalAxis OutputAxis { get; init; }
	public double Scale { get; init; } = 1.0;
	public double Offset { get; init; }
	public required IAxisModifier? Modifier { get; init; }
}