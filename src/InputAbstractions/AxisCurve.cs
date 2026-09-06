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

	/// <summary>
	/// Fraction of the travel (0, 1] at which the curve reaches <see cref="Max"/>; beyond it
	/// the output stays pinned. The curve is evaluated over the saturated travel, so 0.8 puts
	/// the whole response into the first 80% of the deflection. 1.0 (the default) leaves the
	/// input untouched.
	/// </summary>
	public double Saturation
	{
		get;
		init
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(Saturation));
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1.0, nameof(Saturation));
			field = value;
		}
	} = 1.0;

	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsSaturating => Saturation < 1.0 - Tolerance;

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

	// 0 means "not saturating": the input passes through untouched, so a curve without
	// Saturation behaves exactly as it did before the option existed (over-range inputs
	// from a scaled route are still passed on rather than clamped here).
	private static double InverseSaturationOf(AxisCurve curve) =>
		curve.IsSaturating ? 1.0 / curve.Saturation : 0.0;

	private static double Saturate(double input, double inverseSaturation) =>
		inverseSaturation > 0.0
			? Math.Sign(input) * Math.Min(Math.Abs(input) * inverseSaturation, 1.0)
			: input;

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
		private readonly double _InverseSaturation;

		public NonLinearRuntimeModifier(AxisCurve axisCurve)
		{
			if (axisCurve.IsLinear)
			{
				throw new ArgumentException($"Axis curve must not be linear for {nameof(NonLinearRuntimeModifier)}",
					nameof(axisCurve));
			}

			_Curve = axisCurve;
			_InverseSaturation = InverseSaturationOf(axisCurve);
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
		{
			var saturated = Saturate(input, _InverseSaturation);
			if (_Curve.IsLinear)
			{
				return _Curve.Max * saturated;
			}

			return _Curve.Max * Math.Sign(saturated) * Math.Pow(Math.Abs(saturated), _Curve.Exponent);
		}
	}

	private sealed record LinearRuntimeModifier : IRuntimeAxisModifier
	{
		private readonly double _Max;
		private readonly double _InverseSaturation;

		public LinearRuntimeModifier(AxisCurve axisCurve)
		{
			if (!axisCurve.IsLinear)
			{
				throw new ArgumentException($"Axis curve must be linear for {nameof(LinearRuntimeModifier)}",
					nameof(axisCurve));
			}

			_Max = axisCurve.Max;
			_InverseSaturation = InverseSaturationOf(axisCurve);
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update) =>
			_Max * Saturate(input, _InverseSaturation);
	}
}
