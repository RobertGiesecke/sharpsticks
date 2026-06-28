namespace SharpSticks.InputSynthesis.Linux;

/// <summary>
/// Builds <c>struct input_event</c> values for uinput. Pure — the actual write to
/// <c>/dev/uinput</c> lives in <see cref="LinuxUinputSynthesizerDevice"/>.
/// </summary>
internal static class EvdevEvent
{
	// evdev BTN_* mouse codes (&lt;linux/input-event-codes.h&gt;).
	public const ushort BtnLeft = 0x110;
	public const ushort BtnRight = 0x111;
	public const ushort BtnMiddle = 0x112;
	public const ushort BtnSide = 0x113;
	public const ushort BtnExtra = 0x114;

	// evdev REL_* relative axis codes.
	public const ushort RelX = 0x00;
	public const ushort RelY = 0x01;
	public const ushort RelHWheel = 0x06;
	public const ushort RelWheel = 0x08;
	public const ushort RelWheelHiRes = 0x0b;
	public const ushort RelHWheelHiRes = 0x0c;

	/// <summary>High-res wheel value of one detent (kernel convention; matches Windows WHEEL_DELTA).</summary>
	public const int WheelHiResPerNotch = 120;

	public static LinuxInputEvent Key(ushort code, bool down) =>
		new() { Type = EvType.Key, Code = code, Value = down ? 1 : 0 };

	public static LinuxInputEvent Rel(ushort code, int value) =>
		new() { Type = EvType.Rel, Code = code, Value = value };

	/// <summary>The SYN_REPORT that commits the events written since the last one.</summary>
	public static LinuxInputEvent SynReport() =>
		new() { Type = EvType.Syn, Code = LinuxEventCodes.SynReport, Value = 0 };
}
