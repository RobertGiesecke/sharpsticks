namespace ScaledAxisCSharp.DirectInput;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DirectInputDeviceInstanceNative
{
	public uint Size;
	public Guid InstanceGuid;
	public Guid ProductGuid;
	public uint DeviceType;
	public fixed char InstanceName[260];
	public fixed char ProductName[260];
	public Guid ForceFeedbackDriverGuid;
	public ushort UsagePage;
	public ushort Usage;
}