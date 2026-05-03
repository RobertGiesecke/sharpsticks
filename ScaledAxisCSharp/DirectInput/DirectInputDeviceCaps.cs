namespace ScaledAxisCSharp.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputDeviceCaps
{
	public uint Size;
	public uint Flags;
	public uint DeviceType;
	public uint Axes;
	public uint Buttons;
	public uint Povs;
	public uint ForceFeedbackSamplePeriod;
	public uint ForceFeedbackMinTimeResolution;
	public uint FirmwareRevision;
	public uint HardwareRevision;
	public uint ForceFeedbackDriverVersion;
}