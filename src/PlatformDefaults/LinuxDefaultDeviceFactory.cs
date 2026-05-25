using SharpSticks.LinuxInput;
using SharpSticks.LinuxOutput;

namespace SharpSticks.PlatformDefaults;

/// Hard-binds to LinuxInput + uinput-backed output. Mirrors WindowsDefaultDeviceFactory;
/// see that class for the rationale on shipping both unconditionally.
public sealed class LinuxDefaultDeviceFactory(
	IJoystickDeviceFactory<LinuxInputJoystickDevice> joystickDeviceFactory,
	IOutputDeviceFactory<LinuxOutputDevice> outputDeviceFactory)
	: CombinedDeviceFactoryBase<LinuxInputJoystickDevice, LinuxOutputDevice>(
		joystickDeviceFactory, outputDeviceFactory)
{
	public static readonly LinuxDefaultDeviceFactory Instance = new(
		LinuxInputJoystickDeviceFactory.Instance,
		LinuxOutputDeviceFactory.Instance);
}
