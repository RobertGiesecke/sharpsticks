namespace SharpSticks.InputAbstractions;

public abstract record BoundRoute<T> : BoundRoute, IMergeableObject<T>
	where T : BoundRoute<T>
{
	protected abstract T Merge(MergeObjectContext context);
	T IMergeableObject<T>.Merge(MergeObjectContext context) => Merge(context);

	protected override IMergeableObject MergeUntyped(MergeObjectContext context) => Merge(context);
}

public abstract record BoundRoute : IBoundRoute
{
	protected abstract InputBinding InputBinding { get; }
	protected abstract uint OutputDeviceId { get; }

	InputBinding IBoundRoute.InputBinding => InputBinding;
	uint IBoundRoute.OutputDeviceId => OutputDeviceId;
	IMergeableObject IMergeableObject.Merge(MergeObjectContext context) => MergeUntyped(context);
	protected abstract IMergeableObject MergeUntyped(MergeObjectContext context);
}