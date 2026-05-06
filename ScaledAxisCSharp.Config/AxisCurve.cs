namespace ScaledAxisCSharp.Config;

public sealed record AxisCurve : IAxisModifier
{
	public double Max { get; init; } = 1.0;
	private const double Tolerance = 0.000001;
	private const double InitialSteepness = 1.0;

	public double Steepness
	{
		get;
		init
		{
			field = value;
			IsLinear = Math.Abs(value - 1.0) < Tolerance;
			IsFlat = Math.Abs(value) < Tolerance;
		}
	} = InitialSteepness;

	public bool IsLinear { get; private init; } = Math.Abs(InitialSteepness - 1.0) < Tolerance;
	public bool IsFlat { get; private init; } = Math.Abs(InitialSteepness) < Tolerance;

	public double Apply(double input)
	{
		if (IsFlat)
		{
			return 0.0;
		}

		if (IsLinear)
		{
			return Max * input;
		}

		var exponent = Steepness < 1.0 ? 1.0 / Steepness : 2.0 - Steepness;
		return Max * Math.Sign(input) * Math.Pow(Math.Abs(input), exponent);
	}

	double IAxisModifier.Apply(double input,
		IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
		=> Apply(input);
}