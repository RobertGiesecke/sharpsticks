namespace SharpSticks.InputAbstractions;

public sealed record AxisRoute : BoundRoute
{
	public required AxisBinding Source { get; init; }
	public required OutputAxisBinding OutputBinding { get; init; }
	public double Scale { get; init; } = 1.0;
	public double Offset { get; init; }
	public required IAxisModifier? Modifier { get; init; }

	protected override InputBinding InputBinding => Source;
	protected override uint OutputDeviceId => OutputBinding.OutputDeviceId;
}