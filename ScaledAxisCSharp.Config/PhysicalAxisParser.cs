namespace ScaledAxisCSharp.Config;

public static class PhysicalAxisParser
{
	extension(PhysicalAxis)
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

		public static IReadOnlyList<PhysicalAxis> ParseList(string? axisList)
		{
			if (string.IsNullOrWhiteSpace(axisList))
			{
				return
				[
					PhysicalAxis.X,
					PhysicalAxis.Y,
					PhysicalAxis.Z,
					PhysicalAxis.Rx,
					PhysicalAxis.Ry,
					PhysicalAxis.Rz,
					PhysicalAxis.Slider1,
					PhysicalAxis.Slider2,
				];
			}

			return axisList
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(PhysicalAxis.Parse)
				.Distinct()
				.ToArray();
		}

	}
}