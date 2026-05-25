using SharpSticks.DirectInput;
using SharpSticks.VJoy;

namespace SharpSticks.PlatformDefaults;

/// Hard-binds to DirectInput + vJoy. Always compiles regardless of build-host OS —
/// the underlying P/Invokes only execute when methods are actually called. Consumers
/// reach this through the buildTransitive `PlatformDefaultDeviceFactory` alias on
/// Windows, or by name when an OS-runtime dispatch is needed (e.g. the analyzer).
public sealed class WindowsDefaultDeviceFactory(
	IJoystickDeviceFactory<DirectInputJoystickDevice> joystickDeviceFactory,
	IOutputDeviceFactory<VJoyDevice> outputDeviceFactory)
	: CombinedDeviceFactoryBase<DirectInputJoystickDevice, VJoyDevice>(
		joystickDeviceFactory, outputDeviceFactory)
{
	public static readonly WindowsDefaultDeviceFactory Instance = new(
		DirectInputJoystickDeviceFactory.Instance,
		VJoyDeviceFactory.Instance);
}
