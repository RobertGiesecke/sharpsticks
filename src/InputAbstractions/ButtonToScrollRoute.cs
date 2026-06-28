using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an input button to a scroll-wheel increment: one pulse of <see cref="Amount"/>
/// units along <see cref="Axis"/> on each rising edge of the source (edge-triggered, one
/// notch per press). The sign of <see cref="Amount"/> is the direction. Drives the
/// synthesizer directly — it does not go through the vJoy output-button refcount.
/// </summary>
public sealed record ButtonToScrollRoute : IRoute, IMergeableObject<ButtonToScrollRoute>
{
    public required ButtonBinding Source { get; init; }
    public required ScrollAxis Axis { get; init; }
    public int Amount { get; init; } = 1;
    public MouseScrollUnit Unit { get; init; } = MouseScrollUnit.Notch;

    public ButtonToScrollRoute Merge(MergeObjectContext context)
    {
        var hasChanges = false;
        var source = Source.MergeOrGet(context, ref hasChanges);
        return !hasChanges ? this : this with { Source = source };
    }
}
