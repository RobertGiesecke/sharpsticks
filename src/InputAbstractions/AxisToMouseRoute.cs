using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an input axis to mouse movement along one <see cref="MouseDirection"/>.
/// Reuses the joystick <see cref="AxisBinding"/> source + <see cref="IAxisModifier"/>
/// pipeline (deadzone/curves apply). For <see cref="MovementKind.Relative"/> the
/// normalized axis controls pointer speed: <see cref="Sensitivity"/> is the pixels
/// per second at full deflection.
/// </summary>
public sealed record AxisToMouseRoute : IConfigurableRoute, IMergeableObject<AxisToMouseRoute>
{
	public required AxisBinding Source { get; init; }
	public required MouseDirection Direction { get; init; }
	public MouseMovement Movement { get; init; } = MouseMovement.Relative;
	public IAxisModifier? Modifier { get; init; }

	/// <summary>Pixels per second at full deflection (relative movement).</summary>
	public double Sensitivity { get; init; } = 1000.0;

	public AxisToMouseRoute Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var modifier = Modifier?.MergeOrGet(context, ref hasChanges);
		var source = Source.MergeOrGet(context, ref hasChanges);

		return !hasChanges
			? this
			: this with
			{
				Modifier = modifier,
				Source = source,
			};
	}
}
