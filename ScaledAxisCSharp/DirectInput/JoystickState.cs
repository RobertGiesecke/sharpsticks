namespace ScaledAxisCSharp.DirectInput;

internal readonly record struct JoystickState(
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
	public static unsafe JoystickState FromNative(DirectInputJoyState2 state)
	{
		ulong buttonBitsLow = 0;
		ulong buttonBitsHigh = 0;

		for (var index = 0; index < 64; index++)
		{
			if ((state.Buttons[index] & 0x80) != 0)
			{
				buttonBitsLow |= 1UL << index;
			}
		}

		for (var index = 0; index < 64; index++)
		{
			if ((state.Buttons[index + 64] & 0x80) != 0)
			{
				buttonBitsHigh |= 1UL << index;
			}
		}

		return new JoystickState(
			state.X,
			state.Y,
			state.Z,
			state.Rx,
			state.Ry,
			state.Rz,
			state.Sliders[0],
			state.Sliders[1],
			buttonBitsLow,
			buttonBitsHigh);
	}

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