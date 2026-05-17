namespace SharpSticks.InputAbstractions;

public readonly record struct RouteAxisOptions()
{
	public IAxisModifier? Modifier { get; init; }
	public const double DefaultScale = 1.0;
	public double Scale { get; init; } = DefaultScale;
	public double Offset { get; init; }

	/// <summary>
	/// Negates the post-Scale/Offset output (equivalent to <c>Scale=-Scale, Offset=-Offset</c>).
	/// For an unsigned source in <c>[0,1]</c> with the defaults this yields <c>[-1,0]</c>.
	/// </summary>
	public bool Invert { get; init; }
}