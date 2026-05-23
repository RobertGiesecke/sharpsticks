namespace SharpSticks.LinuxOutput;

internal static class LinuxOutputAxisCodes
{
	/// Map a SharpSticks <see cref="Axis"/> to its evdev ABS_* code.
	public static ushort GetAbsCode(Axis axis) => axis switch
	{
		Axis.X => LinuxEventCodes.AbsX,
		Axis.Y => LinuxEventCodes.AbsY,
		Axis.Z => LinuxEventCodes.AbsZ,
		Axis.Rx => LinuxEventCodes.AbsRx,
		Axis.Ry => LinuxEventCodes.AbsRy,
		Axis.Rz => LinuxEventCodes.AbsRz,
		Axis.Slider1 => LinuxEventCodes.AbsThrottle,
		Axis.Slider2 => LinuxEventCodes.AbsRudder,
		_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported axis for uinput."),
	};

	/// Map a SharpSticks 1-based button number to a Linux button code.
	/// Buttons 1..16 land in the joystick range (BTN_TRIGGER..BTN_DEAD).
	/// Buttons 17..56 land in the BTN_TRIGGER_HAPPY range. Beyond that throws.
	public static ushort GetButtonCode(int buttonNumber)
	{
		const ushort btnTriggerHappy = 0x2c0;
		const int maxTriggerHappy = 40;

		if (buttonNumber < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonNumber), buttonNumber,
				"Button numbers are 1-based.");
		}

		if (buttonNumber <= 16)
		{
			return (ushort)(LinuxEventCodes.BtnJoystick + (buttonNumber - 1));
		}

		var offset = buttonNumber - 17;
		if (offset >= maxTriggerHappy)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonNumber), buttonNumber,
				$"Linux output supports up to {16 + maxTriggerHappy} buttons.");
		}

		return (ushort)(btnTriggerHappy + offset);
	}
}
