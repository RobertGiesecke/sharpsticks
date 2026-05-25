namespace SharpSticks.OutputAbstractions;

public interface ICombinedDeviceFactory :
	IJoystickDeviceFactory,
	IOutputDeviceFactory
{ }

public interface ICombinedDeviceFactory<TInput, TOutput> :
	ICombinedDeviceFactory,
	IJoystickDeviceFactory<TInput>,
	IOutputDeviceFactory<TOutput>
	where TInput : JoystickDevice
	where TOutput : OutputDevice
{ }