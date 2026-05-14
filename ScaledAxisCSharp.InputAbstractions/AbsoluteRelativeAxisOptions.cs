namespace ScaledAxisCSharp.InputAbstractions;

public readonly record struct AbsoluteRelativeAxisOptions
{
	public AbsoluteRelativeAxisOptions()
	{
	}

	public required uint OutputDeviceId { get; init; }
	public required PhysicalAxis IncreaseAxis { get; init; }
	public required PhysicalAxis DecreaseAxis { get; init; }
	public double SourceInputMinimum { get; init; } = 0.0;
	public double SourceInputMaximum { get; init; } = 1.0;
	public double IncreaseRestPosition { get; init; } = 0.5;
	public double DecreaseRestPosition { get; init; } = 0.5;
	public double Minimum { get; init; } = 0.0;
	public double Maximum { get; init; } = 1.0;
	public double InitialValue { get; init; } = 0.0;
	public double Gain { get; init; } = 4.0;
	public double MaxOutput { get; init; } = 1.0;
	public double MinOutput { get; init; }
	public double ErrorTolerance { get; init; } = 0.01;
	public double IncreaseEdgeBoost { get; init; }
	public double DecreaseEdgeBoost { get; init; }
	public double OutputRiseRate { get; init; } = 0.05;
	public double OutputFallRate { get; init; } = 0.05;
	public double IncreaseRate { get; init; } = 0.04;
	public double DecreaseRate { get; init; } = 0.04;
}