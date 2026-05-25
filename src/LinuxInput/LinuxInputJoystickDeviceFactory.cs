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

	public ImmutableArray<AvailableInputDevice> EnumerateAvailableInputs()
	{
		try
		{
			var infos = LinuxInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
			var builder = ImmutableArray.CreateBuilder<AvailableInputDevice>(infos.Length);
			foreach (var info in infos)
			{
				builder.Add(new(
					info.DeviceId,
					info.ProductName,
					info.ProductGuid,
					info.Axes,
					(uint)info.ButtonCodes.Length));
			}

			return builder.ToImmutable();
		}
		catch (Exception ex) when (IsExpectedEnumerationFailure(ex))
		{
			return ImmutableArray<AvailableInputDevice>.Empty;
		}
	}

	private static bool IsExpectedEnumerationFailure(Exception exception) =>
		exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
			or FileNotFoundException or FileLoadException or InvalidOperationException;
}