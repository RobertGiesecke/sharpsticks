namespace SharpSticks.OutputAbstractions;

public sealed class CombinedDeviceFactory<TInput, TOutput>(
	IJoystickDeviceFactory<TInput> joystickDeviceFactory,
	IOutputDeviceFactory<TOutput> outputDeviceFactory)
	: CombinedDeviceFactoryBase<TInput, TOutput>(joystickDeviceFactory, outputDeviceFactory)
	where TInput : JoystickDevice
	where TOutput : OutputDevice;