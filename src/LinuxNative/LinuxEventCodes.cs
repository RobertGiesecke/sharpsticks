namespace SharpSticks.LinuxNative;

/// Kernel ABI constants from <c>&lt;linux/input-event-codes.h&gt;</c>. Stable forever — copying them
/// here lets us depend only on libc, not linux-headers. Shared between evdev (input) and uinput (output).
///
/// The flag/op/event-type families live in the enums below (<see cref="EvType"/>, <see cref="OpenFlags"/>,
/// <see cref="EpollEvents"/>, <see cref="EpollCtlOp"/>). The SYN / BTN_* / ABS_* families stay as raw
/// integer codes here: they index capability bitmasks, iterate as range bounds, and feed offset
/// arithmetic — uses an enum only fights.
public static class LinuxEventCodes
{
	// Sync codes
	public const ushort SynReport = 0x00;

	// Button code ranges (subset relevant to joysticks/gamepads)

	/// BTN_TRIGGER / BTN_JOYSTICK base
	public const ushort BtnJoystick = 0x120;

	/// BTN_SOUTH / BTN_GAMEPAD base
	public const ushort BtnGamepad = 0x130;

	/// BTN_TOOL_PEN — start of stylus region, treat as upper joystick bound
	public const ushort BtnDigi = 0x140;

	// Absolute axis codes
	public const ushort AbsX = 0x00;
	public const ushort AbsY = 0x01;
	public const ushort AbsZ = 0x02;
	public const ushort AbsRx = 0x03;
	public const ushort AbsRy = 0x04;
	public const ushort AbsRz = 0x05;
	public const ushort AbsThrottle = 0x06;
	public const ushort AbsRudder = 0x07;

	/// first POV/hat — pair with AbsHat0Y (hat support pending)
	public const ushort AbsHat0X = 0x10;

	public const ushort AbsHat0Y = 0x11;
	public const ushort AbsMax = 0x3f;

	// poll(2)
	public const short PollIn = 0x0001;
	public const short PollOut = 0x0004;
}

/// <c>EV_*</c> event types — the <c>type</c> field of <c>struct input_event</c> and the argument to
/// <c>UI_SET_EVBIT</c> / <c>EVIOCGBIT</c>.
public enum EvType : ushort
{
	Syn = 0x00,
	Key = 0x01,
	Rel = 0x02,
	Abs = 0x03,
	Msc = 0x04,
	Max = 0x1f,
}

/// <c>open(2)</c> flags. <c>O_RDONLY</c> is 0, so it reads as the "no write/extra flags" baseline.
/// Also accepted by <c>epoll_create1</c> (<c>EPOLL_CLOEXEC</c> == <c>O_CLOEXEC</c>).
[Flags]
public enum OpenFlags
{
	ReadOnly = 0x0000,
	WriteOnly = 0x0001,
	NonBlock = 0x0800,
	CloseOnExec = 0x80000,
}

/// <c>EPOLL*</c> event mask bits — the <c>events</c> field of <c>struct epoll_event</c>.
[Flags]
public enum EpollEvents : uint
{
	None = 0,
	In = 0x0001,
	Err = 0x0008,
	Hup = 0x0010,
	EdgeTriggered = 0x80000000u,
}

/// <c>epoll_ctl(2)</c> operations.
public enum EpollCtlOp
{
	Add = 1,
	Del = 2,
	Mod = 3,
}