namespace ScaledAxisCSharp.Config;

public static class AxisBindingParser
{
	extension(AxisBinding)
	{
		public static AxisBinding Parse(AxisInput input)
		{
			ArgumentNullException.ThrowIfNull(input);
			return new AxisBinding(
				input.DeviceId,
				PhysicalAxis.Parse(input.Axis),
				AxisModeParser.Parse(input.Mode),
				input.Invert,
				input.Deadzone);
		}
	}
}