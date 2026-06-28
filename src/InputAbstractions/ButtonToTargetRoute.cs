namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an input button to any <see cref="ButtonTarget"/> — a vJoy output button, a
/// keyboard key, a mouse button, or a scroll increment. The single button→target route:
/// the per-target route types collapse into this, with the <see cref="ButtonTarget"/>
/// deciding the runtime sink.
/// </summary>
public sealed record ButtonToTargetRoute : IRoute, IMergeableObject<ButtonToTargetRoute>
{
    public required ButtonBinding Source { get; init; }
    public required ButtonTarget Target { get; init; }

    public ButtonToTargetRoute Merge(MergeObjectContext context)
    {
        var hasChanges = false;
        var source = Source.MergeOrGet(context, ref hasChanges);
        return !hasChanges ? this : this with { Source = source };
    }
}
