using System.Runtime.InteropServices;

namespace SharpSticks.LinuxInput;

/// `struct input_event` on 64-bit Linux. timeval = (long tv_sec, long tv_usec).
[StructLayout(LayoutKind.Sequential)]
internal struct LinuxInputEvent
{
	public long TvSec;
	public long TvUsec;
	public ushort Type;
	public ushort Code;
	public int Value;

	public static int Size => sizeof(long) * 2 + sizeof(ushort) * 2 + sizeof(int);
}

/// `struct input_id`: bus/vendor/product/version.
[StructLayout(LayoutKind.Sequential)]
internal struct LinuxInputId
{
	public ushort BusType;
	public ushort Vendor;
	public ushort Product;
	public ushort Version;

	public const int Size = sizeof(ushort) * 4;
}

/// `struct input_absinfo`: min/max/value/fuzz/flat/resolution.
[StructLayout(LayoutKind.Sequential)]
internal struct LinuxAbsInfo
{
	public int Value;
	public int Minimum;
	public int Maximum;
	public int Fuzz;
	public int Flat;
	public int Resolution;

	public const int Size = sizeof(int) * 6;
}

/// `struct pollfd`.
[StructLayout(LayoutKind.Sequential)]
internal struct LinuxPollFd
{
	public int Fd;
	public short Events;
	public short Revents;
}
