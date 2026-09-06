namespace SharpSticks.VJoy;

public static class VJoyAxisConstants
{
	public const uint X = 0x30;
	public const uint Y = 0x31;
	public const uint Z = 0x32;
	public const uint Rx = 0x33;
	public const uint Ry = 0x34;
	public const uint Rz = 0x35;
	public const uint Slider1 = 0x36;
	public const uint Slider2 = 0x37;

	/// Inverse of <see cref="VJoyAxisExtensions.GetVJoyAxisId"/>; false for every HID usage
	/// SharpSticks has no <see cref="Axis"/> for (POVs, the 2.2.x extended axes, ...).
	public static bool TryGetAxis(uint vjoyAxisId, out Axis axis)
	{
		switch (vjoyAxisId)
		{
			case X: axis = Axis.X; return true;
			case Y: axis = Axis.Y; return true;
			case Z: axis = Axis.Z; return true;
			case Rx: axis = Axis.Rx; return true;
			case Ry: axis = Axis.Ry; return true;
			case Rz: axis = Axis.Rz; return true;
			case Slider1: axis = Axis.Slider1; return true;
			case Slider2: axis = Axis.Slider2; return true;
			default: axis = default; return false;
		}
	}
}
