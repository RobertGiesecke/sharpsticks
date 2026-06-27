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
}
