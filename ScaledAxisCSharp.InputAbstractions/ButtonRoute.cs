namespace ScaledAxisCSharp.InputAbstractions;

public sealed record ButtonRoute(ButtonBinding Binding, OutputButtonBinding OutputBinding) : Route
{
	protected override InputBinding InputBinding => Binding;
	protected override uint OutputDeviceId => OutputBinding.OutputDeviceId;
}