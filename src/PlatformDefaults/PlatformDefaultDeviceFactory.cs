namespace SharpSticks.PlatformDefaults;

#if SHARPSTICKS_HAS_INPUT_DEVICE_TYPE && SHARPSTICKS_HAS_OUTPUT_DEVICE_TYPE
public sealed class PlatformDefaultDeviceFactory(
	IJoystickDeviceFactory<PlatformDefaultInputDevice> joystickDeviceFactory,
	IOutputDeviceFactory<PlatformDefaultOutputDevice> outputDeviceFactory)
	: CombinedDeviceFactoryBase<PlatformDefaultInputDevice, PlatformDefaultOutputDevice>(
		joystickDeviceFactory, outputDeviceFactory)
{
	public static readonly PlatformDefaultDeviceFactory Instance = new(
		PlatformDefaultInputDeviceFactory.Instance,
		PlatformDefaultOutputDeviceFactory.Instance);
}
#endif