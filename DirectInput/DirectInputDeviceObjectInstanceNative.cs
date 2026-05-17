namespace ScaledAxisCSharp.DirectInput;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DirectInputDeviceObjectInstanceNative
{
	public uint Size;
	public Guid TypeGuid;
	public uint Offset;
	public uint Type;
	public uint Flags;
	public fixed char Name[260];
	public uint ForceFeedbackMaxForce;
	public uint ForceFeedbackForceResolution;
	public ushort CollectionNumber;
	public ushort DesignatorIndex;
	public ushort UsagePage;
	public ushort Usage;
	public uint Dimension;
	public ushort Exponent;
	public ushort ReportId;
}