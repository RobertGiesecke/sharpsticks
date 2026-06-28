namespace SharpSticks.InputAbstractions;

/// <summary>
/// Source-button-driven macro route. Edge-triggers <see cref="OnPress"/> when
/// <see cref="Binding"/> transitions from released to pressed, and
/// <see cref="OnRelease"/> on the inverse edge. Press and release runs share a
/// single FIFO so event order is preserved.
/// </summary>
public sealed record ButtonMacroRoute : IBoundRoute, IConfigurableRoute
{
	public required ButtonBinding Binding { get; init; }
	public ImmutableArray<IMacroAction> OnPress { get; init; } = [];
	public ImmutableArray<IMacroAction> OnRelease { get; init; } = [];

	public const MacroReentry DefaultReentry = MacroReentry.QueueUntilDone;
	public MacroReentry Reentry { get; init; } = DefaultReentry;

	InputBinding IBoundRoute.InputBinding => Binding;

	public IMergeableObject Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var x1 = Binding.MergeOrGet(context, ref hasChanges);

		var mergeOrGetAllOptions = new MergeableObjectExtensions.MergeOrGetAllOptions
		{
			ReturnUniqueItems = false,
		};
		var x2 = OnPress.MergeOrGetAll(context, ref hasChanges, mergeOrGetAllOptions);
		var x3 = OnRelease.MergeOrGetAll(context, ref hasChanges, mergeOrGetAllOptions);

		return !hasChanges
			? this
			: this with
			{
				Binding = x1,
				OnPress = x2,
				OnRelease = x3,
			};
	}
}