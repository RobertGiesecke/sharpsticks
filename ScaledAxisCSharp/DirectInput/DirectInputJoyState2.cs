namespace ScaledAxisCSharp.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DirectInputJoyState2
{
	public int X;
	public int Y;
	public int Z;
	public int Rx;
	public int Ry;
	public int Rz;
	public fixed int Sliders[2];
	public fixed uint Povs[4];
	public fixed byte Buttons[128];
	public fixed int ExtendedAxes[24];
}