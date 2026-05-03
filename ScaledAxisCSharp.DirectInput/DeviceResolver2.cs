using Collections.Pooled;

namespace ScaledAxisCSharp.DirectInput;

public static class DeviceExtensions
{
	public static AxisBinding BindAxis(this JoystickDevice device, PhysicalAxis physicalAxis)
	{
		return new AxisBinding(device.DeviceId, physicalAxis, AxisMode.Signed, false, 0.0);
	}

	public static ButtonBinding BindButton(this JoystickDevice device, int sourceButton)
	{
		return new ButtonBinding(device.DeviceId, sourceButton);
	}
	
	public static JoystickDevice ResolveDevice(this IReadOnlyList<JoystickDevice> devices, string productName)
	{
		if (string.IsNullOrWhiteSpace(productName))
		{
			throw new InvalidOperationException("DeviceName is required.");
		}

		var exactMatches = devices
			.Where(device => string.Equals(device.Name, productName, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (exactMatches.Length == 1)
		{
			return exactMatches[0];
		}

		if (exactMatches.Length > 1)
		{
			throw new InvalidOperationException(
				$"Multiple joystick devices match '{productName}'. Use a more specific device name.");
		}

		var partialMatches = devices
			.Where(device => device.Name.Contains(productName, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (partialMatches.Length == 1)
		{
			return partialMatches[0];
		}

		if (partialMatches.Length > 1)
		{
			throw new InvalidOperationException(
				$"Multiple joystick devices partially match '{productName}'. Use the full product name from the 'list' command.");
		}

		throw new InvalidOperationException($"No joystick device matched '{productName}'.");
	}

	public static Dictionary<int, JoystickDevice> CollectDevices(this IReadOnlyList<JoystickDevice> devices,
		IEnumerable<int> deviceIds)
	{
		using var byId = devices.ToPooledDictionary(device => device.DeviceId);
		var selected = new Dictionary<int, JoystickDevice>();

		foreach (var deviceId in deviceIds.Distinct())
		{
			if (!byId.TryGetValue(deviceId, out var device))
			{
				throw new InvalidOperationException(
					$"Configured joystick {deviceId} is not available via DirectInput.");
			}

			selected[deviceId] = device;
		}

		return selected;
	}
}