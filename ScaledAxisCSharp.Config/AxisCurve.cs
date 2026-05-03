namespace ScaledAxisCSharp.Config;

public sealed record AxisCurve : IAxisModifier
{
	public double Max { get; init; } = 1.0;
	public double Steepness { get; init; } = 1.0;

	public double Apply(double input)
	{
		if (Steepness == 0.0)
		{
			return 0.0;
		}

		var exponent = Steepness <= 1.0 ? 1.0 / Steepness : 2.0 - Steepness;
		return Max * Math.Sign(input) * Math.Pow(Math.Abs(input), exponent);
	}

	double IAxisModifier.Apply(
		double input,
		IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
		=> Apply(input);
}