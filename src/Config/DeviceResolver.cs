namespace SharpSticks.Config;

public static class DeviceResolver
{
	public static AxisBinding ResolveAxisBinding<TInputDevice>(
		IReadOnlyList<TInputDevice> devices,
		DeviceAxisSource source)
		where TInputDevice : JoystickDevice
	{
		var device = devices.ResolveDevice(source.DeviceName);
		return device.BindAxis(source.Axis);
	}

	public static ButtonBinding ResolveButtonBinding<TInputDevice>(IReadOnlyList<TInputDevice> devices,
		DeviceButtonSource source)
		where TInputDevice : JoystickDevice
	{
		if (source.Button < 1)
		{
			throw new InvalidOperationException("ITB button sources are 1-based.");
		}

		var device = devices.ResolveDevice(source.DeviceName);
		return device.BindButton(source.Button);
	}
}