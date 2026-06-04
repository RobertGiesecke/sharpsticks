namespace SharpSticks.InputAbstractions;

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

	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsLinear { get; private init; } = Math.Abs(InitialSteepness - 1.0) < Tolerance;

	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsFlat { get; private init; } = Math.Abs(InitialSteepness) < Tolerance;

	public void FillDevices(ICollection<int> deviceIds)
	{
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice
	{
		return this switch
		{
			{ IsFlat: true } => FlatRuntimeModifier.Instance,
			{ IsLinear: true } => new LinearRuntimeModifier(this),
			_ => new NonLinearRuntimeModifier(this)
		};
	}

	private sealed record FlatRuntimeModifier : IRuntimeAxisModifier
	{
		public static FlatRuntimeModifier Instance { get; } = new();

		private FlatRuntimeModifier()
		{
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
		{
			return 0.0;
		}
	}

	private sealed record NonLinearRuntimeModifier : IRuntimeAxisModifier
	{
		private readonly AxisCurve _Curve;

		public NonLinearRuntimeModifier(AxisCurve axisCurve)
		{
			if (axisCurve.IsLinear)
			{
				throw new ArgumentException($"Axis curve must not be linear for {nameof(NonLinearRuntimeModifier)}",
					nameof(axisCurve));
			}

			_Curve = axisCurve;
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
		{
			if (_Curve.IsLinear)
			{
				return _Curve.Max * input;
			}

			var steepness = _Curve.Steepness;
			var exponent = steepness < 1.0 ? 1.0 / steepness : 2.0 - steepness;
			return _Curve.Max * Math.Sign(input) * Math.Pow(Math.Abs(input), exponent);
		}
	}

	private sealed record LinearRuntimeModifier : IRuntimeAxisModifier
	{
		private readonly double _Max;

		public LinearRuntimeModifier(AxisCurve axisCurve)
		{
			if (!axisCurve.IsLinear)
			{
				throw new ArgumentException($"Axis curve must be linear for {nameof(LinearRuntimeModifier)}",
					nameof(axisCurve));
			}

			_Max = axisCurve.Max;
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update) => _Max * input;
	}
}