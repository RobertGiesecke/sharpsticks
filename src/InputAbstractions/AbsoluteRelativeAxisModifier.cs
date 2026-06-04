namespace SharpSticks.InputAbstractions;

internal sealed record AbsoluteRelativeAxisModifier : IAxisModifier
{
	private readonly SharedState _SharedState;
	private readonly RelativeDirection _Direction;
	private readonly double _RestPosition;

	public AbsoluteRelativeAxisModifier(SharedState sharedState, RelativeDirection direction, double restPosition)
	{
		_SharedState = sharedState;
		_Direction = direction;
		_RestPosition = Math.Clamp(restPosition, 0.0, 1.0);
	}

	public void FillDevices(ICollection<int> deviceIds)
	{
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new RuntimeModifier(_SharedState, _Direction, _RestPosition);

	internal sealed class SharedState
	{
		private readonly AbsoluteRelativeAxisOptions _Options;
		private readonly double _Minimum;
		private readonly double _Maximum;

		public SharedState(AbsoluteRelativeAxisOptions options)
		{
			_Options = options;
			_Minimum = Math.Min(options.Minimum, options.Maximum);
			_Maximum = Math.Max(options.Minimum, options.Maximum);
			Current = Clamp(options.InitialValue);
		}

		public AbsoluteRelativeAxisOptions Options => _Options;
		public double Current { get; private set; }
		public double LastSourceInput { get; private set; }
		public double LastTarget { get; private set; }
		public double LastError { get; private set; }
		public double LastCurrentBefore { get; private set; }
		public double LastCurrentAfter { get; private set; }
		public double LastDesiredIncrease { get; private set; }
		public double LastDesiredDecrease { get; private set; }
		public double LastActualIncrease { get; private set; }
		public double LastActualDecrease { get; private set; }
		public RelativeDirection? LastActiveDirection { get; private set; }
		public double LastIncreaseBias { get; private set; } = 1.0;
		public double LastDecreaseBias { get; private set; } = 1.0;

		public double GetDesiredPulseMagnitude(double input, RelativeDirection direction)
		{
			var target = MapInputToTarget(input);
			var error = target - Current;
			LastSourceInput = input;
			LastTarget = target;
			LastError = error;
			LastCurrentBefore = Current;
			LastCurrentAfter = Current;
			if (Math.Abs(error) <= _Options.ErrorTolerance)
			{
				LastActiveDirection = null;
				SetDesired(direction, 0.0);
				return 0.0;
			}

			var isIncrease = error > 0.0;
			LastActiveDirection = isIncrease ? RelativeDirection.Increase : RelativeDirection.Decrease;
			if ((direction == RelativeDirection.Increase) != isIncrease)
			{
				SetDesired(direction, 0.0);
				return 0.0;
			}

			var output = Math.Abs(error) * _Options.Gain;
			var normalizedTarget = NormalizeTarget(target);
			var edgeBias = direction switch
			{
				RelativeDirection.Increase => 1.0 + Math.Max(0.0, _Options.IncreaseEdgeBoost) * normalizedTarget,
				RelativeDirection.Decrease =>
					1.0 + Math.Max(0.0, _Options.DecreaseEdgeBoost) * (1.0 - normalizedTarget),
				_ => 1.0,
			};
			if (direction == RelativeDirection.Increase)
			{
				LastIncreaseBias = edgeBias;
				LastDecreaseBias = 1.0 + Math.Max(0.0, _Options.DecreaseEdgeBoost) * (1.0 - normalizedTarget);
			}
			else
			{
				LastDecreaseBias = edgeBias;
				LastIncreaseBias = 1.0 + Math.Max(0.0, _Options.IncreaseEdgeBoost) * normalizedTarget;
			}

			output *= edgeBias;
			var outputMagnitude = Math.Min(output, Math.Abs(_Options.MaxOutput));
			if (outputMagnitude <= 0.0)
			{
				SetDesired(direction, 0.0);
				return 0.0;
			}

			outputMagnitude = Math.Max(outputMagnitude, Math.Abs(_Options.MinOutput));
			SetDesired(direction, outputMagnitude);
			return outputMagnitude;
		}

		public void Advance(double input, RelativeDirection direction, double actualPulseMagnitude)
		{
			SetActual(direction, actualPulseMagnitude);
			var target = MapInputToTarget(input);
			var error = target - Current;
			if (Math.Abs(error) <= _Options.ErrorTolerance)
			{
				Current = target;
				LastCurrentAfter = Current;
				return;
			}

			var isIncrease = error > 0.0;
			if ((direction == RelativeDirection.Increase) != isIncrease)
			{
				return;
			}

			var rate = isIncrease ? _Options.IncreaseRate : _Options.DecreaseRate;
			if (rate <= 0.0 || actualPulseMagnitude <= 0.0)
			{
				LastCurrentAfter = Current;
				return;
			}

			var step = actualPulseMagnitude * rate;
			if (step > Math.Abs(error))
			{
				step = Math.Abs(error);
			}

			Current = Clamp(Current + (isIncrease ? step : -step));
			LastCurrentAfter = Current;
		}

		private void SetDesired(RelativeDirection direction, double value)
		{
			if (direction == RelativeDirection.Increase)
			{
				LastDesiredIncrease = value;
			}
			else
			{
				LastDesiredDecrease = value;
			}
		}

		private void SetActual(RelativeDirection direction, double value)
		{
			if (direction == RelativeDirection.Increase)
			{
				LastActualIncrease = value;
			}
			else
			{
				LastActualDecrease = value;
			}
		}

		private double NormalizeTarget(double target)
		{
			if (_Maximum <= _Minimum)
			{
				return 0.0;
			}

			return Math.Clamp((target - _Minimum) / (_Maximum - _Minimum), 0.0, 1.0);
		}

		private double MapInputToTarget(double input)
		{
			var sourceMin = _Options.SourceInputMinimum;
			var sourceMax = _Options.SourceInputMaximum;
			if (sourceMax <= sourceMin)
			{
				return Clamp(_Minimum);
			}

			var normalizedInput = Math.Clamp((input - sourceMin) / (sourceMax - sourceMin), 0.0, 1.0);
			return Clamp(_Minimum + normalizedInput * (_Maximum - _Minimum));
		}

		private double Clamp(double value) => Math.Clamp(value, _Minimum, _Maximum);
	}

	private sealed record RuntimeModifier :
		StatefulRuntimeInputModifier<double, RuntimeModifier.PulseState>,
		IRuntimeAxisModifier,
		IRuntimeAxisDebugView
	{
		internal struct PulseState
		{
			public double CurrentPulseMagnitude;
		}

		private readonly SharedState _SharedState;
		private readonly RelativeDirection _Direction;
		private readonly double _RestPosition;
		private readonly double _OutputRiseRate;
		private readonly double _OutputFallRate;
		private readonly bool _IsDebugOwner;

		public RuntimeModifier(SharedState sharedState, RelativeDirection direction, double restPosition)
		{
			_SharedState = sharedState;
			_Direction = direction;
			_RestPosition = restPosition;
			_OutputRiseRate = Math.Max(sharedState.Options.OutputRiseRate, 0.0);
			_OutputFallRate = Math.Max(sharedState.Options.OutputFallRate, 0.0);
			_IsDebugOwner = direction == RelativeDirection.Decrease;
		}

		protected override double Apply(double input, JoystickState?[] states, ref PulseState state, ApplyMode mode)
		{
			// GetDesiredPulseMagnitude only records debug-view fields. The
			// per-instance pulse magnitude lives in the state struct (peeks
			// run on a copy); SharedState is shared with the paired direction
			// and sits outside the struct, so it is only advanced on real
			// frames.
			var desiredPulseMagnitude = _SharedState.GetDesiredPulseMagnitude(input, _Direction);
			state.CurrentPulseMagnitude = Slew(state.CurrentPulseMagnitude, desiredPulseMagnitude);
			if (mode == ApplyMode.Update)
			{
				_SharedState.Advance(input, _Direction, state.CurrentPulseMagnitude);
			}

			return MapPulseToSignedOutput(_RestPosition, state.CurrentPulseMagnitude);
		}

		public string? GetDebugView()
		{
			if (!_IsDebugOwner)
			{
				return null;
			}

			return
				$"absrel src={FormatDouble(_SharedState.LastSourceInput)} target={FormatDouble(_SharedState.LastTarget)} current={FormatDouble(_SharedState.LastCurrentBefore)}->{FormatDouble(_SharedState.LastCurrentAfter)} err={FormatDouble(_SharedState.LastError)} active={_SharedState.LastActiveDirection?.ToString() ?? "none"} bias+={FormatDouble(_SharedState.LastIncreaseBias)} desired+={FormatDouble(_SharedState.LastDesiredIncrease)} actual+={FormatDouble(_SharedState.LastActualIncrease)} bias-={FormatDouble(_SharedState.LastDecreaseBias)} desired-={FormatDouble(_SharedState.LastDesiredDecrease)} actual-={FormatDouble(_SharedState.LastActualDecrease)}";
		}

		private static double MapPulseToSignedOutput(double restPosition, double pulseMagnitude)
		{
			var clampedRest = Math.Clamp(restPosition, 0.0, 1.0);
			var clampedPulse = Math.Clamp(pulseMagnitude, 0.0, 1.0);
			var restSigned = clampedRest * 2.0 - 1.0;
			return restSigned + clampedPulse * (1.0 - restSigned);
		}

		private double Slew(double current, double desired)
		{
			if (desired > current)
			{
				return MoveTowards(current, desired, _OutputRiseRate);
			}

			return MoveTowards(current, desired, _OutputFallRate);
		}

		private static double MoveTowards(double current, double desired, double maxStep)
		{
			if (maxStep <= 0.0)
			{
				return desired;
			}

			var delta = desired - current;
			if (Math.Abs(delta) <= maxStep)
			{
				return desired;
			}

			return current + Math.CopySign(maxStep, delta);
		}

		private static string FormatDouble(double value) => value.ToString("0.0000");
	}

	internal enum RelativeDirection
	{
		Increase,
		Decrease,
	}
}