namespace SharpSticks.DirectInput;

public sealed class DirectInputJoystickDeviceFactory : IJoystickDeviceFactory<DirectInputJoystickDevice>
{
	public static readonly DirectInputJoystickDeviceFactory Instance = new();

	public PooledList<DirectInputJoystickDevice> EnumerateConnectedInputDevices()
	{
		var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
		var deviceInfos = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);

		var devices = new PooledList<DirectInputJoystickDevice>(deviceInfos.Length);
		try
		{
			foreach (var deviceInfo in deviceInfos)
			{
				var device = DirectInputJoystickDevice.OpenDevice(directInput, deviceInfo);
				if (device is not null)
				{
					devices.Add(device);
				}
			}

			return devices;
		}
		catch
		{
			DirectInputJoystickDevice.DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}
}