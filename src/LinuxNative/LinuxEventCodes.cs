namespace SharpSticks.LinuxNative;

/// Kernel ABI constants from <c>&lt;linux/input-event-codes.h&gt;</c>. Stable forever — copying them
/// here lets us depend only on libc, not linux-headers. Shared between evdev (input) and uinput (output).
public static class LinuxEventCodes
{
	// Event types
	public const ushort EvSyn = 0x00;
	public const ushort EvKey = 0x01;
	public const ushort EvAbs = 0x03;
	public const ushort EvMsc = 0x04;
	public const ushort EvMax = 0x1f;

	// Sync codes
	public const ushort SynReport = 0x00;

	// Button code ranges (subset relevant to joysticks/gamepads)
	public const ushort BtnJoystick = 0x120;     // BTN_TRIGGER / BTN_JOYSTICK base
	public const ushort BtnGamepad = 0x130;      // BTN_SOUTH / BTN_GAMEPAD base
	public const ushort BtnDigi = 0x140;         // BTN_TOOL_PEN — start of stylus region, treat as upper joystick bound

	// Absolute axis codes
	public const ushort AbsX = 0x00;
	public const ushort AbsY = 0x01;
	public const ushort AbsZ = 0x02;
	public const ushort AbsRx = 0x03;
	public const ushort AbsRy = 0x04;
	public const ushort AbsRz = 0x05;
	public const ushort AbsThrottle = 0x06;
	public const ushort AbsRudder = 0x07;
	public const ushort AbsHat0X = 0x10;
	public const ushort AbsHat0Y = 0x11;
	public const ushort AbsMax = 0x3f;

	// open(2) flags
	public const int OReadOnly = 0x0000;
	public const int OWriteOnly = 0x0001;
	public const int ONonBlock = 0x0800;
	public const int OCloseOnExec = 0x80000;

	// poll(2)
	public const short PollIn = 0x0001;
	public const short PollOut = 0x0004;

	// epoll(2)
	public const uint EpollIn = 0x0001;
	public const uint EpollErr = 0x0008;
	public const uint EpollHup = 0x0010;
	public const uint EpollEt = 0x80000000u;
	public const int EpollCtlAdd = 1;
	public const int EpollCtlDel = 2;
	public const int EpollCtlMod = 3;
}
