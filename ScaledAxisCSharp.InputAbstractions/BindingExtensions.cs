namespace ScaledAxisCSharp.InputAbstractions;

public static class BindingExtensions
{
	public static AxisRoute RouteToSameAxisOnVJoy(
		this AxisBinding binding,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(binding.Axis, scale, offset, modifier);

	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		PhysicalAxis vJoyAxis,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = binding,
			VJoyAxis = vJoyAxis,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};

	public static ButtonRoute RouteButton(this ButtonBinding binding, int targetButton) => new(binding, targetButton);
}