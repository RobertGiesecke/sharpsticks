namespace ScaledAxisCSharp;

internal static class PhysicalAxisParser
{
	public static PhysicalAxis Parse(string value)
	{
		return value.Trim().ToLowerInvariant() switch
		{
			"x" => PhysicalAxis.X,
			"y" => PhysicalAxis.Y,
			"z" => PhysicalAxis.Z,
			"r" or "rx" => PhysicalAxis.R,
			"u" or "slider1" => PhysicalAxis.U,
			"v" or "slider2" => PhysicalAxis.V,
			_ => throw new InvalidOperationException($"Unsupported physical axis '{value}'. Use x, y, z, r, u or v."),
		};
	}
}