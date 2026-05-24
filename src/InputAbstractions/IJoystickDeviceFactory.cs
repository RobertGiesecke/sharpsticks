namespace SharpSticks.InputAbstractions;

public interface IJoystickDeviceFactory
{
	PooledList<JoystickDevice> EnumerateConnectedInputDevices();
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