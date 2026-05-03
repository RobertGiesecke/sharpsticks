namespace ScaledAxisCSharp.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputPropertyHeader
{
	public uint Size;
	public uint HeaderSize;
	public uint Object;
	public uint How;
}