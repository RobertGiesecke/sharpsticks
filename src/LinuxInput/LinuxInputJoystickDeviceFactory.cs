namespace SharpSticks.LinuxInput;

public sealed class LinuxInputJoystickDeviceFactory : IJoystickDeviceFactory<LinuxInputJoystickDevice>
{
	public static readonly LinuxInputJoystickDeviceFactory Instance = new();

	public PooledList<LinuxInputJoystickDevice> EnumerateConnectedInputDevices()
	{
		var infos = LinuxInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
		var devices = new PooledList<LinuxInputJoystickDevice>(infos.Length);
		try
		{
			foreach (var info in infos)
			{
				var device = LinuxInputJoystickDevice.OpenDevice(info);
				if (device is not null)
				{
					devices.Add(device);
				}
			}

			return devices;
		}
		catch
		{
			LinuxInputJoystickDevice.DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}
}