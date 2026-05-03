namespace ScaledAxisCSharp.Config;

public sealed class AxisMapping
{
	public AxisInput Source { get; set; } = new();
	public string TargetAxis { get; set; } = "x";
	public double Scale { get; set; } = 1.0;
	public double Offset { get; set; }
}