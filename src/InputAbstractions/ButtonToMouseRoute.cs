using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// Routes an input button to a synthesized mouse button: while the source is held,
/// the mouse button is held. Several sources targeting the same
/// <see cref="OutputMouseButton"/> OR together (any held holds it). Drives the
/// synthesizer directly — it does not go through the vJoy output-button refcount.
/// </summary>
public sealed record ButtonToMouseRoute : IRoute, IMergeableObject<ButtonToMouseRoute>
{
	public required ButtonBinding Source { get; init; }
	public required OutputMouseButton Button { get; init; }

	public ButtonToMouseRoute Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var source = Source.MergeOrGet(context, ref hasChanges);
		return !hasChanges ? this : this with { Source = source };
	}
}
