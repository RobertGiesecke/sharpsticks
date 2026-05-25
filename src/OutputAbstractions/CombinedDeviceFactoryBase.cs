namespace SharpSticks.OutputAbstractions;

public abstract class CombinedDeviceFactoryBase<TInput, TOutput> : ICombinedDeviceFactory<TInput, TOutput>
	where TInput : JoystickDevice
	where TOutput : OutputDevice
{
	private readonly IJoystickDeviceFactory<TInput> _JoystickDeviceFactory;
	private readonly IOutputDeviceFactory<TOutput> _OutputDeviceFactory;

	protected CombinedDeviceFactoryBase(
		IJoystickDeviceFactory<TInput> joystickDeviceFactory,
		IOutputDeviceFactory<TOutput> outputDeviceFactory)
	{
		_JoystickDeviceFactory =
			joystickDeviceFactory ?? throw new ArgumentNullException(nameof(joystickDeviceFactory));
		_OutputDeviceFactory = outputDeviceFactory ?? throw new ArgumentNullException(nameof(outputDeviceFactory));
	}

	public PooledList<TInput> EnumerateConnectedInputDevices() =>
		_JoystickDeviceFactory.EnumerateConnectedInputDevices();

	public PooledList<TOutput> EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs) =>
		_OutputDeviceFactory.EnumerateConnectedOutputDevices(requests, availableInputs);
}