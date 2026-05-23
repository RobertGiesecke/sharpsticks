namespace SharpSticks.LinuxNative;

/// Linux ioctl number encoding helper. Encodes (direction, type, nr, size) into a 32-bit
/// request number per <c>&lt;asm-generic/ioctl.h&gt;</c>.
public static class LinuxIoctl
{
	public const uint DirNone = 0;
	public const uint DirWrite = 1;
	public const uint DirRead = 2;

	private const int NrShift = 0;
	private const int TypeShift = 8;
	private const int SizeShift = 16;
	private const int DirShift = 30;

	public static uint Encode(uint dir, uint type, uint nr, uint size)
	{
		return (dir << DirShift) | (size << SizeShift) | (type << TypeShift) | (nr << NrShift);
	}

	public static uint Iow(uint type, uint nr, uint size) => Encode(DirWrite, type, nr, size);
	public static uint Ior(uint type, uint nr, uint size) => Encode(DirRead, type, nr, size);
}
