namespace SharpSticks.Config;

public static class AxisParser
{
	extension(Axis)
	{
		public static Axis Parse(string value)
		{
			return value.Trim().ToLowerInvariant() switch
			{
				"x" => Axis.X,
				"y" => Axis.Y,
				"z" => Axis.Z,
				"r" or "rx" => Axis.Rx,
				"ry" => Axis.Ry,
				"rz" => Axis.Rz,
				"u" or "slider1" => Axis.Slider1,
				"v" or "slider2" => Axis.Slider2,
				_ => throw new InvalidOperationException(
					$"Unsupported physical axis '{value}'. Use x, y, z, rx, ry, rz, slider1 or slider2."),
			};
		}

		public static IReadOnlyList<Axis> ParseList(string? axisList)
		{
			if (string.IsNullOrWhiteSpace(axisList))
			{
				return
				[
					Axis.X,
					Axis.Y,
					Axis.Z,
					Axis.Rx,
					Axis.Ry,
					Axis.Rz,
					Axis.Slider1,
					Axis.Slider2,
				];
			}

			return axisList
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(Axis.Parse)
				.Distinct()
				.ToArray();
		}

	}
}