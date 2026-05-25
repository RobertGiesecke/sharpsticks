namespace SharpSticks.InputAbstractions;

public interface IJoystickDeviceFactory
{
	PooledList<JoystickDevice> EnumerateConnectedInputDevices();

	/// Non-claiming metadata snapshot of every input device the platform knows about.
	/// Used at design time (e.g. by the source generator) where opening + acquiring
	/// every device would be wasteful and may have side effects. Default impl returns
	/// empty for backends that don't support pre-acquire discovery.
	ImmutableArray<AvailableInputDevice> EnumerateAvailableInputs() => ImmutableArray<AvailableInputDevice>.Empty;
}

public interface IJoystickDeviceFactory<T> : IJoystickDeviceFactory
	where T : JoystickDevice
{
	new PooledList<T> EnumerateConnectedInputDevices();

	PooledList<JoystickDevice> IJoystickDeviceFactory.EnumerateConnectedInputDevices()
	{
		using var list = EnumerateConnectedInputDevices();
		var result = new PooledList<JoystickDevice>(list.Count);
		try
		{
			foreach (var device in list)
			{
				result.Add(device);
			}

			return result;
		}
		catch
		{
			result.Dispose();
			throw;
		}
	}
}