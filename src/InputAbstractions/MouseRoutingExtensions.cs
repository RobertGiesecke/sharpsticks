using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

public static class MouseRoutingExtensions
{
	/// <summary>
	/// Route this axis to mouse movement along <paramref name="direction"/>. For
	/// relative movement <paramref name="sensitivity"/> is pixels per second at full
	/// deflection.
	/// </summary>
	public static AxisToMouseRoute RouteToMouse(
		this AxisBinding source,
		MouseDirection direction,
		MouseMovement? movement = null,
		double sensitivity = 1000.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = source,
			Direction = direction,
			Movement = movement ?? new()
			{
				Kind = MovementKind.Relative,
			},
			Sensitivity = sensitivity,
			Modifier = modifier,
		};

	/// <summary>Route this button to a synthesized mouse button (held while the source is held).</summary>
	public static ButtonToMouseRoute RouteToMouse(this ButtonBinding source, OutputMouseButton button) =>
		new()
		{
			Source = source,
			Button = button,
		};

	/// <summary>
	/// Route this axis to mouse-wheel scrolling along <paramref name="axis"/>. The
	/// normalized axis controls scroll speed: <paramref name="sensitivity"/> is notches
	/// per second at full deflection.
	/// </summary>
	public static AxisToScrollRoute RouteToScroll(
		this AxisBinding source,
		ScrollAxis axis,
		MouseScrollUnit unit = MouseScrollUnit.Notch,
		double sensitivity = 10.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = source,
			Axis = axis,
			Unit = unit,
			Sensitivity = sensitivity,
			Modifier = modifier,
		};

	/// <summary>Route this button to a scroll increment: one pulse per press in <paramref name="direction"/>.</summary>
	public static ButtonToScrollRoute RouteToScroll(
		this ButtonBinding source,
		ScrollDirection direction,
		int amount = 1,
		MouseScrollUnit unit = MouseScrollUnit.Notch)
	{
		var (axis, signed) = ScrollDirectionMap.Resolve(direction, amount);
		return new()
		{
			Source = source,
			Axis = axis,
			Amount = signed,
			Unit = unit,
		};
	}
}
