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

	public ImmutableArray<AvailableInputDevice> EnumerateAvailableInputs()
	{
		try
		{
			var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
			var deviceInfos = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);
			var builder = ImmutableArray.CreateBuilder<AvailableInputDevice>(deviceInfos.Length);
			foreach (var info in deviceInfos)
			{
				DirectInputCapabilityReader.TryGetCapabilities(
					directInput, info.InstanceGuid, out var axes, out var buttonCount);
				builder.Add(new(info.DeviceId, info.ProductName, info.ProductGuid, axes, buttonCount));
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