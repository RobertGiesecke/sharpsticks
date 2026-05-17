namespace ScaledAxisCSharp.InputAbstractions;

public sealed record ButtonRoute(ButtonBinding Binding, OutputButtonBinding OutputBinding) : BoundRoute
{
	protected override InputBinding InputBinding => Binding;
	protected override uint OutputDeviceId => OutputBinding.OutputDeviceId;
}