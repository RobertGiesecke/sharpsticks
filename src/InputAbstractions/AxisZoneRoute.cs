namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an axis zone to any <see cref="ButtonTarget"/> with <see cref="AxisZoneTriggerMode"/>
/// semantics (hold while in range, or pulse on entering). The single axis-zone→target
/// route: the per-target zone route types collapse into this. A scroll target pulses on
/// entering the zone; level targets (vJoy/key/mouse) are held while in range.
/// </summary>
public sealed record AxisZoneRoute : IConfigurableRoute, IMergeableObject<AxisZoneRoute>
{
    public required AxisBinding Source { get; init; }
    public required ButtonTarget Target { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }

    /// <summary>If <c>true</c> the zone is <c>[Min, Max]</c>; otherwise <c>[Min, Max)</c>.</summary>
    public bool IncludeMax { get; init; } = true;

    public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;

    /// <summary>Only used when <see cref="Mode"/> is <see cref="AxisZoneTriggerMode.Pulse"/>.</summary>
    public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);

    public AxisZoneRoute Merge(MergeObjectContext context)
    {
        var hasChanges = false;
        var source = Source.MergeOrGet(context, ref hasChanges);
        return !hasChanges ? this : this with { Source = source };
    }
}
