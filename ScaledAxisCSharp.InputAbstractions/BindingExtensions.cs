namespace ScaledAxisCSharp.InputAbstractions;

public static class BindingExtensions
{
	public static AxisRoute RouteToSameAxisOnVJoy(
		this AxisBinding binding,
		int vJoyDeviceId,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(vJoyDeviceId, binding.Axis, scale, offset, modifier);

	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		int vJoyDeviceId,
		PhysicalAxis vJoyAxis,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = binding,
			VJoyDeviceId = vJoyDeviceId,
			VJoyAxis = vJoyAxis,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};

	public static ButtonRoute RouteButton(this ButtonBinding binding, int vJoyDeviceId, int targetButton) =>
		new(binding, vJoyDeviceId, targetButton);
}