namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputObjectDataFormat
{
	public nint GuidPointer;
	public uint Offset;
	public uint Type;
	public uint Flags;
}