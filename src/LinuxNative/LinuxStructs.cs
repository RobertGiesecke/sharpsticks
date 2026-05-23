using System.Runtime.InteropServices;

namespace SharpSticks.LinuxNative;

/// <c>struct input_event</c> on 64-bit Linux. timeval = (long tv_sec, long tv_usec).
/// Used for both reading from evdev devices and writing to uinput devices.
[StructLayout(LayoutKind.Sequential)]
public struct LinuxInputEvent
{
	public long TvSec;
	public long TvUsec;
	public ushort Type;
	public ushort Code;
	public int Value;

	public static int Size => sizeof(long) * 2 + sizeof(ushort) * 2 + sizeof(int);
}

/// <c>struct input_id</c>: bus/vendor/product/version. Shared by evdev and uinput.
[StructLayout(LayoutKind.Sequential)]
public struct LinuxInputId
{
	public ushort BusType;
	public ushort Vendor;
	public ushort Product;
	public ushort Version;

	public const int Size = sizeof(ushort) * 4;
}

/// <c>struct input_absinfo</c>: min/max/value/fuzz/flat/resolution. Returned by EVIOCGABS.
[StructLayout(LayoutKind.Sequential)]
public struct LinuxAbsInfo
{
	public int Value;
	public int Minimum;
	public int Maximum;
	public int Fuzz;
	public int Flat;
	public int Resolution;

	public const int Size = sizeof(int) * 6;
}

/// <c>struct pollfd</c>.
[StructLayout(LayoutKind.Sequential)]
public struct LinuxPollFd
{
	public int Fd;
	public short Events;
	public short Revents;
}
