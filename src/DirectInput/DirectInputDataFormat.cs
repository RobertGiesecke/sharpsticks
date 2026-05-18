namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputDataFormat
{
	public uint Size;
	public uint ObjectSize;
	public uint Flags;
	public uint DataSize;
	public uint ObjectCount;
	public nint ObjectDataFormats;
}