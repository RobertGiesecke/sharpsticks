namespace ScaledAxisCSharp.VJoy;

internal static class VJoyAxisParser
{
	public static VJoyAxis Parse(string value)
	{
		return value.Trim().ToLowerInvariant() switch
		{
			"x" => VJoyAxis.X,
			"y" => VJoyAxis.Y,
			"z" => VJoyAxis.Z,
			"rx" => VJoyAxis.Rx,
			"ry" => VJoyAxis.Ry,
			"rz" => VJoyAxis.Rz,
			"slider1" or "u" => VJoyAxis.Slider1,
			"slider2" or "v" => VJoyAxis.Slider2,
			_ => throw new InvalidOperationException(
				$"Unsupported vJoy axis '{value}'. Use x, y, z, rx, ry, rz, slider1 or slider2."),
		};
	}
}