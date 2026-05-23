namespace SharpSticks.LinuxInput;

/// Kernel ABI constants from <linux/input-event-codes.h>. Stable forever — copying them
/// here lets us depend only on libc, not linux-headers.
internal static class LinuxInputEventCodes
{
	// Event types
	public const ushort EvSyn = 0x00;
	public const ushort EvKey = 0x01;
	public const ushort EvAbs = 0x03;
	public const ushort EvMsc = 0x04;
	public const ushort EvMax = 0x1f;

	// Sync codes
	public const ushort SynReport = 0x00;

	// Button codes (subset relevant to joysticks/gamepads)
	public const ushort BtnJoystick = 0x120;     // BTN_TRIGGER / BTN_JOYSTICK base
	public const ushort BtnLastJoystick = 0x12f; // up to BTN_DEAD
	public const ushort BtnGamepad = 0x130;      // BTN_SOUTH / BTN_GAMEPAD base
	public const ushort BtnLastGamepad = 0x13e;
	public const ushort BtnDigi = 0x140;         // BTN_TOOL_PEN — stylus, treat as boundary

	// Absolute axes
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
	public const int ONonBlock = 0x0800;
	public const int OCloseOnExec = 0x80000;

	// poll(2)
	public const short PollIn = 0x0001;

	// ioctl direction bits
	private const uint IocNone = 0;
	private const uint IocWrite = 1;
	private const uint IocRead = 2;
	private const int IocNrShift = 0;
	private const int IocTypeShift = 8;
	private const int IocSizeShift = 16;
	private const int IocDirShift = 30;

	private const byte EvIocType = (byte)'E';

	private static uint Ioc(uint dir, uint type, uint nr, uint size)
	{
		return (dir << IocDirShift) | (size << IocSizeShift) | (type << IocTypeShift) | (nr << IocNrShift);
	}

	public static uint EviocgId => Ioc(IocRead, EvIocType, 0x02, (uint)LinuxInputId.Size);
	public static uint EviocgName(uint length) => Ioc(IocRead, EvIocType, 0x06, length);
	public static uint EviocgUniq(uint length) => Ioc(IocRead, EvIocType, 0x08, length);
	public static uint EviocgBit(uint ev, uint length) => Ioc(IocRead, EvIocType, 0x20 + ev, length);
	public static uint EviocgAbs(uint absCode) => Ioc(IocRead, EvIocType, 0x40u + absCode, (uint)LinuxAbsInfo.Size);
}
