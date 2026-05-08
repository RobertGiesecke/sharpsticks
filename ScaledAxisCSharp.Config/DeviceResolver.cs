namespace ScaledAxisCSharp.Config;

public static class DeviceResolver
{
	public static AxisBinding ResolveAxisBinding(IReadOnlyList<JoystickDevice> devices, DeviceAxisSource source)
	{
		var device = devices.ResolveDevice(source.DeviceName);
		return device.BindAxis(source.Axis);
	}

	public static ButtonBinding ResolveButtonBinding(IReadOnlyList<JoystickDevice> devices, DeviceButtonSource source)
	{
		if (source.Button < 1)
		{
			throw new InvalidOperationException("ITB button sources are 1-based.");
		}

		var device = devices.ResolveDevice(source.DeviceName);
		return device.BindButton(source.Button);
	}
}