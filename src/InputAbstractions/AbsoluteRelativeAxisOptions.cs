namespace SharpSticks.InputAbstractions;

public readonly record struct AbsoluteRelativeAxisOptions()
{
	/// <summary>
	/// May be the same as <see cref="DecreaseAxis"/>: the route then becomes a
	/// single bidirectional axis that rests at center, pulsing positive on
	/// increase and negative on decrease (the rest-position options are not
	/// used in that mode).
	/// </summary>
	public required OutputAxisBinding IncreaseAxis { get; init; }

	/// <inheritdoc cref="IncreaseAxis"/>
	public required OutputAxisBinding DecreaseAxis { get; init; }
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
	public double ErrorTolerance { get; init; } = 0.001;
	public double IncreaseEdgeBoost { get; init; }
	public double DecreaseEdgeBoost { get; init; }
	public double OutputRiseRate { get; init; } = 0.05;
	public double OutputFallRate { get; init; } = 0.05;
	public double IncreaseRate { get; init; } = 0.04;
	public double DecreaseRate { get; init; } = 0.04;
}