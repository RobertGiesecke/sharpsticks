using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Config;

public sealed class AxisMapping
{
	public AxisInput Source { get; set; } = new();
	public required string TargetAxis { get; set; }
	public double Scale { get; set; } = 1.0;
	public double Offset { get; set; }
	public IAxisModifier? Modifier { get; set; }
}