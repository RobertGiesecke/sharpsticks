namespace ScaledAxisCSharp.Config;

public sealed class ScaledAxisMapping
{
	public AxisInput ValueSource { get; set; } = new();

	public AxisInput FactorSource { get; set; } = new()
	{
		Mode = "unsigned",
	};

	public string TargetAxis { get; set; } = "x";
	public double FactorLow { get; set; } = 0.5;
	public double FactorHigh { get; set; } = 1.0;
	public double OutputScale { get; set; } = 1.0;
	public double OutputOffset { get; set; }
}