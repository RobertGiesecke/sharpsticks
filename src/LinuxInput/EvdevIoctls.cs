using SharpSticks.LinuxNative;

namespace SharpSticks.LinuxInput;

/// evdev-specific ioctl request numbers. The shared LinuxIoctl encoder lives in LinuxNative;
/// this file just names the evdev requests we use.
internal static class EvdevIoctls
{
	private const uint IocType = 'E';

	public static uint EviocgId => LinuxIoctl.Ior(IocType, 0x02, (uint)LinuxInputId.Size);
	public static uint EviocgName(uint length) => LinuxIoctl.Ior(IocType, 0x06, length);
	public static uint EviocgUniq(uint length) => LinuxIoctl.Ior(IocType, 0x08, length);
	public static uint EviocgBit(uint ev, uint length) => LinuxIoctl.Ior(IocType, 0x20 + ev, length);
	public static uint EviocgAbs(uint absCode) => LinuxIoctl.Ior(IocType, 0x40u + absCode, (uint)LinuxAbsInfo.Size);
}
