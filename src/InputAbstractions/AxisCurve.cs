namespace SharpSticks.InputAbstractions;

public sealed record AxisCurve : IAxisModifier
{
	public double Max { get; init; } = 1.0;
	private const double Tolerance = 0.000001;
	private const double InitialExponent = 1.0;

	/// <summary>
	/// Power-curve exponent: output = <see cref="Max"/> · sign(input) · |input|^Exponent.
	/// 1.0 is linear; above 1 damps the center response (ease-out, e.g. a
	/// game's "curve 2.4" setting); between 0 and 1 boosts it (ease-in).
	/// Must be positive.
	/// </summary>
	public double Exponent
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(Exponent));
			field = value;
			IsLinear = Math.Abs(value - 1.0) < Tolerance;
		}
	} = InitialExponent;

	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsLinear { get; private init; } = Math.Abs(InitialExponent - 1.0) < Tolerance;

	/// <summary>A curve with <see cref="Max"/> 0 outputs 0 for every input.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsFlat => Math.Abs(Max) < Tolerance;

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

			return _Curve.Max * Math.Sign(input) * Math.Pow(Math.Abs(input), _Curve.Exponent);
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
