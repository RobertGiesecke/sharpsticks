namespace SharpSticks.LinuxOutput;

/// uinput ioctl request numbers and constants from <c>&lt;linux/uinput.h&gt;</c>.
internal static class LinuxUinput
{
	public const string DevicePath = "/dev/uinput";
	public const int MaxNameSize = 80;

	private const uint IocType = (uint)'U';

	public static uint UiDevCreate => LinuxIoctl.Encode(LinuxIoctl.DirNone, IocType, 1, 0);
	public static uint UiDevDestroy => LinuxIoctl.Encode(LinuxIoctl.DirNone, IocType, 2, 0);
	public static uint UiDevSetup => LinuxIoctl.Iow(IocType, 3, LinuxUinputSetup.Size);
	public static uint UiAbsSetup => LinuxIoctl.Iow(IocType, 4, LinuxUinputAbsSetup.Size);
	public static uint UiSetEvBit => LinuxIoctl.Iow(IocType, 100, sizeof(int));
	public static uint UiSetKeyBit => LinuxIoctl.Iow(IocType, 101, sizeof(int));
	public static uint UiSetAbsBit => LinuxIoctl.Iow(IocType, 103, sizeof(int));

	// Bus types used in struct input_id.
	public const ushort BusVirtual = 0x06;
}

/// <c>struct uinput_setup</c>. 8 + 80 + 4 = 92 bytes.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LinuxUinputSetup
{
	public LinuxInputId Id;
	public fixed byte Name[LinuxUinput.MaxNameSize];
	public uint FfEffectsMax;

	public const uint Size = 8u + LinuxUinput.MaxNameSize + 4u;
}

/// <c>struct uinput_abs_setup</c>. u16 code + 2 bytes alignment + 24-byte absinfo = 28 bytes.
[StructLayout(LayoutKind.Sequential)]
internal struct LinuxUinputAbsSetup
{
	public ushort Code;
	private ushort _Padding;
	public LinuxAbsInfo AbsInfo;

	public const uint Size = 4u + (uint)LinuxAbsInfo.Size;
}

/// uinput-specific ioctl P/Invokes (the struct-taking ones; the int and no-arg variants live in LinuxLibc).
internal static partial class LinuxUinputNative
{
	private const string Libc = "libc";

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlUinputSetup(int fd, nuint request, ref LinuxUinputSetup value);

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlUinputAbsSetup(int fd, nuint request, ref LinuxUinputAbsSetup value);
}
