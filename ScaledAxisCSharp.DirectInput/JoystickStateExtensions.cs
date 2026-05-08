namespace ScaledAxisCSharp.DirectInput;

public static class JoystickStateExtensions
{
	extension(JoystickState)
	{
		internal static unsafe JoystickState FromNative(DirectInputJoyState2 state)
		{
			ulong buttonBitsLow = 0;
			ulong buttonBitsHigh = 0;

			for (var index = 0; index < 64; index++)
				if ((state.Buttons[index] & 0x80) != 0)
				{
					buttonBitsLow |= 1UL << index;
				}

			for (var index = 0; index < 64; index++)
				if ((state.Buttons[index + 64] & 0x80) != 0)
				{
					buttonBitsHigh |= 1UL << index;
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
	}
}