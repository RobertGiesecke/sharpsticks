namespace ScaledAxisCSharp.InputAbstractions;

public readonly record struct JoystickState(
	int X,
	int Y,
	int Z,
	int Rx,
	int Ry,
	int Rz,
	int Slider1,
	int Slider2,
	ulong ButtonBitsLow,
	ulong ButtonBitsHigh)
{
	public int GetAxisValue(PhysicalAxis axis)
	{
		return axis switch
		{
			PhysicalAxis.X => X,
			PhysicalAxis.Y => Y,
			PhysicalAxis.Z => Z,
			PhysicalAxis.Rx => Rx,
			PhysicalAxis.Ry => Ry,
			PhysicalAxis.Rz => Rz,
			PhysicalAxis.Slider1 => Slider1,
			PhysicalAxis.Slider2 => Slider2,
			_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
		};
	}

	public bool IsButtonPressed(int buttonNumber)
	{
		if (buttonNumber < 1 || buttonNumber > 128)
		{
			return false;
		}

		var zeroBasedIndex = buttonNumber - 1;
		if (zeroBasedIndex < 64)
		{
			return ((ButtonBitsLow >> zeroBasedIndex) & 1UL) != 0;
		}

		return ((ButtonBitsHigh >> (zeroBasedIndex - 64)) & 1UL) != 0;
	}
}