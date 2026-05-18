namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputPropertyRange
{
	public DirectInputPropertyHeader Header;
	public int Min;
	public int Max;
}