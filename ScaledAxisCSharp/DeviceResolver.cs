namespace ScaledAxisCSharp;

public static class DeviceResolver
{
	public static AxisBinding ResolveAxisBinding(IReadOnlyList<JoystickDevice> devices, DeviceAxisSource source)
	{
		var device = devices.ResolveDevice(source.DeviceName);
		return device.BindAxis(source.Axis);
	}

	public static AxisRoute RouteAxis(this AxisBinding binding, VJoyAxis targetAxis, double scale, double offset, IAxisModifier? modifier = null)
	{
		return new AxisRoute
		{
			Source = binding,
			TargetAxis = targetAxis,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};
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

	public static ButtonRoute RouteButton(this ButtonBinding binding, int targetButton)
	{
		return new ButtonRoute(binding, targetButton);
	}
}