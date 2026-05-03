namespace ScaledAxisCSharp.DirectInput;

internal static class PhysicalAxisParser
{
	public static PhysicalAxis Parse(string value)
	{
		return value.Trim().ToLowerInvariant() switch
		{
			"x" => PhysicalAxis.X,
			"y" => PhysicalAxis.Y,
			"z" => PhysicalAxis.Z,
			"r" or "rx" => PhysicalAxis.Rx,
			"ry" => PhysicalAxis.Ry,
			"rz" => PhysicalAxis.Rz,
			"u" or "slider1" => PhysicalAxis.Slider1,
			"v" or "slider2" => PhysicalAxis.Slider2,
			_ => throw new InvalidOperationException(
				$"Unsupported physical axis '{value}'. Use x, y, z, rx, ry, rz, slider1 or slider2."),
		};
	}
}
