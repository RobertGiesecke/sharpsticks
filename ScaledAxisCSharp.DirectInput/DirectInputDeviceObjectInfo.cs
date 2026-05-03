namespace ScaledAxisCSharp.DirectInput;

internal readonly record struct DirectInputDeviceObjectInfo(Guid TypeGuid, uint Offset, uint Type)
{
	public static unsafe DirectInputDeviceObjectInfo FromNative(DirectInputDeviceObjectInstanceNative* native)
	{
		return new DirectInputDeviceObjectInfo(native->TypeGuid, native->Offset, native->Type);
	}
}