using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an input axis to mouse-wheel scrolling along one <see cref="ScrollAxis"/>.
/// Reuses the joystick <see cref="AxisBinding"/> source + <see cref="IAxisModifier"/>
/// pipeline (deadzone/curves apply). The normalized axis controls scroll speed:
/// <see cref="Sensitivity"/> is notches per second at full deflection (regardless of
/// <see cref="Unit"/> — <see cref="MouseScrollUnit.HighResolution"/> just emits finer steps).
/// </summary>
public sealed record AxisToScrollRoute : IRoute, IMergeableObject<AxisToScrollRoute>
{
    public required AxisBinding Source { get; init; }
    public required ScrollAxis Axis { get; init; }
    public MouseScrollUnit Unit { get; init; } = MouseScrollUnit.Notch;

    /// <summary>Notches per second at full deflection.</summary>
    public double Sensitivity { get; init; } = 10.0;

    public IAxisModifier? Modifier { get; init; }

    public AxisToScrollRoute Merge(MergeObjectContext context)
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
