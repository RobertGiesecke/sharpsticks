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
				Axis.Parse(input.Axis),
				AxisMode.Parse(input.Mode),
				input.Invert,
				input.Deadzone);
		}
	}
}