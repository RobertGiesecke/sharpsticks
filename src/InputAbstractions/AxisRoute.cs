namespace SharpSticks.InputAbstractions;

public sealed record AxisRoute : BoundRoute<AxisRoute>
{
	public required AxisBinding Source { get; init; }
	public required OutputAxisBinding OutputBinding { get; init; }
	public double Scale { get; init; } = 1.0;
	public double Offset { get; init; }
	public required IAxisModifier? Modifier { get; init; }

	protected override InputBinding InputBinding => Source;
	protected override uint OutputDeviceId => OutputBinding.OutputDeviceId;

	protected override AxisRoute Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var x1 = Modifier?.MergeOrGet(context, ref hasChanges);
		var x2 = Source.MergeOrGet(context, ref hasChanges);
		var x3 = OutputBinding.MergeOrGet(context, ref hasChanges);

		return !hasChanges
			? this
			: this with
			{
				Modifier = x1,
				Source = x2,
				OutputBinding = x3,
			};
	}
}