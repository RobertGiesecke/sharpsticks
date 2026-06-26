namespace SharpSticks.InputAbstractions;

public sealed record ButtonRoute(ButtonBinding Binding, OutputButtonBinding OutputBinding) : BoundRoute<ButtonRoute>
{
	protected override InputBinding InputBinding => Binding;
	protected override uint OutputDeviceId => OutputBinding.OutputDeviceId;

	protected override ButtonRoute Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var x1 = Binding.MergeOrGet(context, ref hasChanges);
		var x2 = OutputBinding.MergeOrGet(context, ref hasChanges);
		return !hasChanges
			? this
			: this with
			{
				Binding = x1,
				OutputBinding = x2,
			};
	}
}