namespace ScaledAxisCSharp;

internal sealed record AxisBinding(int DeviceId, PhysicalAxis Axis, AxisMode Mode, bool Invert, double Deadzone)
{
	public static AxisBinding Parse(AxisInput input)
	{
		ArgumentNullException.ThrowIfNull(input);
		return new AxisBinding(
			input.DeviceId,
			PhysicalAxisParser.Parse(input.Axis),
			AxisModeParser.Parse(input.Mode),
			input.Invert,
			input.Deadzone);
	}
}