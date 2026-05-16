namespace ScaledAxisCSharp.InputAbstractions;

public abstract record BoundRoute : IBoundRoute
{
	protected abstract InputBinding InputBinding { get; }
	protected abstract uint OutputDeviceId { get; }

	InputBinding IBoundRoute.InputBinding => InputBinding;
	uint IBoundRoute.OutputDeviceId => OutputDeviceId;
}