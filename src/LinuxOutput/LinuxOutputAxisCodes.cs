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

	// Usable joystick-button code segments, in the exact order Linux joydev/SDL assign button
	// indices: they scan [BTN_JOYSTICK, KEY_MAX) ascending first, then wrap to [BTN_MISC,
	// BTN_JOYSTICK). A button's in-game index is its RANK among the declared codes, so laying the
	// segments out in this order keeps button N == the Nth declared button. Verified against
	// <linux/input-event-codes.h>: these are the button codes that neither reclassify a device
	// carrying ABS axes (mouse 0x110, tablet 0x140, keyboard KEY_*) nor get folded into a hat
	// (BTN_DPAD_*, which SDL synthesizes into a POV instead of buttons). The counts line up with
	// the OutputButtonCapacity tiers: 32 (joystick) + 40 (trigger-happy) + 16 (misc) = 88.
	private static readonly (ushort Start, int Count)[] ButtonSegments =
	[
		(LinuxEventCodes.BtnJoystick, LinuxEventCodes.BtnDigi - LinuxEventCodes.BtnJoystick),
		(LinuxEventCodes.BtnTriggerHappy, LinuxEventCodes.BtnTriggerHappyEnd - LinuxEventCodes.BtnTriggerHappy),
		(LinuxEventCodes.BtnMisc, LinuxEventCodes.BtnMiscEnd - LinuxEventCodes.BtnMisc),
	];

	// Sum of the segment lengths above, as a compile-time constant so LinuxOutputDevice can expose
	// it as a const MaxButtonCount (attribute arguments must be constant). Keep the operands in
	// sync with ButtonSegments.
	public const uint MaxButtons =
		(LinuxEventCodes.BtnDigi - LinuxEventCodes.BtnJoystick)
		+ (LinuxEventCodes.BtnTriggerHappyEnd - LinuxEventCodes.BtnTriggerHappy)
		+ (LinuxEventCodes.BtnMiscEnd - LinuxEventCodes.BtnMisc);

	/// Map a SharpSticks 1-based button number to a Linux button code, walking the usable
	/// segments in button-index order (see <see cref="ButtonSegments"/>). Throws past 88.
	public static ushort GetButtonCode(int buttonNumber)
	{
		if (buttonNumber < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonNumber), buttonNumber,
				"Button numbers are 1-based.");
		}

		var index = buttonNumber - 1;
		foreach (var (start, count) in ButtonSegments)
		{
			if (index < count)
			{
				return (ushort)(start + index);
			}

			index -= count;
		}

		throw new ArgumentOutOfRangeException(nameof(buttonNumber), buttonNumber,
			$"Linux output supports up to {MaxButtons} buttons.");
	}
}
