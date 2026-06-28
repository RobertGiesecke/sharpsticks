using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// A scroll-wheel increment as a <see cref="ButtonTarget"/>: one pulse of
/// <see cref="Amount"/> units along <see cref="Axis"/> per press. The sign of
/// <see cref="Amount"/> is the direction (positive = up / right).
/// </summary>
public sealed record ScrollTarget : ButtonTarget
{
	public required ScrollAxis Axis { get; init; }
	public int Amount { get; init; } = 1;
	public MouseScrollUnit Unit { get; init; } = MouseScrollUnit.Notch;

	/// <summary>Build a target from a discrete <see cref="ScrollDirection"/> and a magnitude.</summary>
	public static ScrollTarget Towards(
		ScrollDirection direction, int magnitude = 1, MouseScrollUnit unit = MouseScrollUnit.Notch)
	{
		var (axis, amount) = ScrollDirectionMap.Resolve(direction, magnitude);
		return new() { Axis = axis, Amount = amount, Unit = unit };
	}

	public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
		new ScrollSink(RequireSynthesizer(context), Axis, Amount, Unit);
}