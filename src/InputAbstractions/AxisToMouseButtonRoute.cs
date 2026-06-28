using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// Drives a synthesized mouse button from a source-axis zone [<see cref="Min"/>,
/// <see cref="Max"/>), with the same <see cref="AxisZoneTriggerMode"/> semantics as a
/// vJoy-button zone (<c>Hold</c> while in range, or <c>Pulse</c> for
/// <see cref="PulseDuration"/> on entering). Several sources targeting the same
/// <see cref="OutputMouseButton"/> — physical buttons and axis zones alike — OR
/// together. Produced by <c>SplitIntoButtons</c> for a <see cref="MouseButtonTarget"/>
/// zone. Drives the synthesizer directly (not the vJoy output-button refcount).
/// </summary>
public sealed record AxisToMouseButtonRoute : IRoute, IMergeableObject<AxisToMouseButtonRoute>
{
    public required AxisBinding Source { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public bool IncludeMax { get; init; }
    public required OutputMouseButton Button { get; init; }
    public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;
    public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);

    public AxisToMouseButtonRoute Merge(MergeObjectContext context)
    {
        var hasChanges = false;
        var source = Source.MergeOrGet(context, ref hasChanges);
        return !hasChanges ? this : this with { Source = source };
    }
}
