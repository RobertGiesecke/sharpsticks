using System.Globalization;
using System.Text;

namespace SharpSticks.InputAbstractions;

internal sealed record AbsoluteRelativeAxisModifier : IAxisModifier
{
	private readonly SharedState _SharedState;

	// null = bidirectional: both directions on one output axis, resting at
	// center, increase pulsing positive and decrease negative.
	private readonly RelativeDirection? _Direction;
	private readonly double _RestPosition;

	public static (AbsoluteRelativeAxisModifier Increase, AbsoluteRelativeAxisModifier Decrease) Create(
		AbsoluteRelativeAxisOptions options)
	{
		var sharedState = new SharedState(options);
		var increaseModifier = new AbsoluteRelativeAxisModifier(
			sharedState,
			RelativeDirection.Increase,
			options.IncreaseRestPosition);
		var decreaseModifier = new AbsoluteRelativeAxisModifier(
			sharedState,
			RelativeDirection.Decrease,
			options.DecreaseRestPosition);
		return (increaseModifier, decreaseModifier);
	}

	/// <summary>
	/// Single-axis variant for when Increase and Decrease share one output
	/// axis: increase pulses push the output positive, decrease pulses
	/// negative, and it rests at center (0). The rest-position options are
	/// not used in this mode.
	/// </summary>
	public static IAxisModifier CreateBidirectional(AbsoluteRelativeAxisOptions options) =>
		new AbsoluteRelativeAxisModifier(new(options), direction: null, restPosition: 0.5);

	private AbsoluteRelativeAxisModifier(SharedState sharedState, RelativeDirection? direction, double restPosition)
	{
		_SharedState = sharedState;
		_Direction = direction;
		_RestPosition = Math.Clamp(restPosition, 0.0, 1.0);
	}

	void IFillDevices.FillDevices(ICollection<int> deviceIds)
	{
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		_Direction is { } direction
			? new DualAxesRuntimeModifier(_SharedState, direction, _RestPosition)
			: new BidirectionalRuntimeModifier(_SharedState);

	private enum RelativeDirection
	{
		Increase,
		Decrease,
	}

	private sealed class SharedState
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

		/// <summary>
		/// Bidirectional-mode integration: <paramref name="netPulse"/> is the
		/// signed net deflection the consumer actually saw (positive =
		/// increase). Unlike <see cref="Advance"/> this also integrates
		/// wrong-direction transients (a decaying opposite pulse after a
		/// direction flip still moves the consumer), so the model tracks the
		/// consumer instead of standing still while it drifts.
		/// </summary>
		public void AdvanceNet(double input, double netPulse)
		{
			SetActual(RelativeDirection.Increase, Math.Max(netPulse, 0.0));
			SetActual(RelativeDirection.Decrease, Math.Max(-netPulse, 0.0));
			var target = MapInputToTarget(input);
			var error = target - Current;
			if (Math.Abs(error) <= _Options.ErrorTolerance)
			{
				Current = target;
				LastCurrentAfter = Current;
				return;
			}

			var rate = netPulse > 0.0 ? _Options.IncreaseRate : _Options.DecreaseRate;
			if (netPulse == 0.0 || rate <= 0.0)
			{
				LastCurrentAfter = Current;
				return;
			}

			var step = netPulse * rate;

			// Moving toward the target must not overshoot it (mirrors Advance);
			// moving away from it integrates as-is — the consumer moved, so
			// the model follows.
			if (Math.Sign(step) == Math.Sign(error) && Math.Abs(step) > Math.Abs(error))
			{
				step = error;
			}

			Current = Clamp(Current + step);
			LastCurrentAfter = Current;
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

	private abstract record RuntimeModifier<TState> : StatefulRuntimeInputModifier<double, TState>
		where TState : struct
	{
		protected SharedState SharedState { get; }

		protected RuntimeModifier(SharedState sharedState)
		{
			SharedState = sharedState;
		}

		protected static double Slew(double current, double desired, double riseRate, double fallRate) =>
			MoveTowards(
				current,
				desired,
				desired > current
					? riseRate
					: fallRate);

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

		protected static string FormatDebugView(SharedState sharedState) =>
			$"absrel src={sharedState.LastSourceInput} " +
			$"target={sharedState.LastTarget} " +
			$"current={sharedState.LastCurrentBefore}->{sharedState.LastCurrentAfter} " +
			$"err={sharedState.LastError} " +
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
			$"active={sharedState.LastActiveDirection switch {
				RelativeDirection.Decrease => "decrease",
				RelativeDirection.Increase => "increase",
				null => "none",
			}} " +
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
			$"bias+={sharedState.LastIncreaseBias} " +
			$"desired+={sharedState.LastDesiredIncrease} " +
			$"actual+={sharedState.LastActualIncrease} " +
			$"bias-={sharedState.LastDecreaseBias} " +
			$"desired-={sharedState.LastDesiredDecrease} " +
			$"actual-={sharedState.LastActualDecrease}";
	}

	private sealed record BidirectionalRuntimeModifier :
		RuntimeModifier<BidirectionalRuntimeModifier.PulseState>,
		IRuntimeAxisModifier,
		IRuntimeAxisDebugView
	{
		internal struct PulseState
		{
			public double IncreasePulse;
			public double DecreasePulse;
		}

		private readonly double _OutputRiseRate;
		private readonly double _OutputFallRate;

		public BidirectionalRuntimeModifier(SharedState sharedState) : base(sharedState)
		{
			_OutputRiseRate = Math.Max(sharedState.Options.OutputRiseRate, 0.0);
			_OutputFallRate = Math.Max(sharedState.Options.OutputFallRate, 0.0);
		}

		protected override double Apply(double input, JoystickState?[] states, ref PulseState state, ApplyMode mode)
		{
			// Both directions evaluate against the one SharedState; only the
			// direction matching the error sign produces a non-zero desired
			// pulse, the other decays at the fall rate. SharedState sits
			// outside the state struct, so it is only advanced on real frames.
			var desiredIncrease = SharedState.GetDesiredPulseMagnitude(input, RelativeDirection.Increase);
			var desiredDecrease = SharedState.GetDesiredPulseMagnitude(input, RelativeDirection.Decrease);
			state.IncreasePulse = Slew(state.IncreasePulse, desiredIncrease, _OutputRiseRate, _OutputFallRate);
			state.DecreasePulse = Slew(state.DecreasePulse, desiredDecrease, _OutputRiseRate, _OutputFallRate);

			// Rest is center: increase pulses push positive, decrease negative.
			// The consumer only ever sees this NET deflection — during a
			// direction flip the decaying opposite pulse cancels part of the
			// rising one — so the model must integrate the net as well, or it
			// races ahead of what the consumer actually received.
			var net = Math.Clamp(state.IncreasePulse, 0.0, 1.0) - Math.Clamp(state.DecreasePulse, 0.0, 1.0);
			if (mode == ApplyMode.Update)
			{
				SharedState.AdvanceNet(input, net);
			}

			return net;
		}

		public string? GetDebugView() => FormatDebugView(SharedState);
	}

	private sealed record DualAxesRuntimeModifier :
		RuntimeModifier<DualAxesRuntimeModifier.PulseState>,
		IRuntimeAxisModifier,
		IRuntimeAxisDebugView
	{
		internal struct PulseState
		{
			public double CurrentPulseMagnitude;
		}

		private readonly RelativeDirection _Direction;
		private readonly double _RestPosition;
		private readonly double _OutputRiseRate;
		private readonly double _OutputFallRate;
		private readonly bool _IsDebugOwner;

		public DualAxesRuntimeModifier(SharedState sharedState, RelativeDirection direction, double restPosition)
			: base(sharedState)
		{
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
			var desiredPulseMagnitude = SharedState.GetDesiredPulseMagnitude(input, _Direction);
			state.CurrentPulseMagnitude = Slew(state.CurrentPulseMagnitude, desiredPulseMagnitude, _OutputRiseRate,
				_OutputFallRate);
			if (mode == ApplyMode.Update)
			{
				SharedState.Advance(input, _Direction, state.CurrentPulseMagnitude);
			}

			return MapPulseToSignedOutput(_RestPosition, state.CurrentPulseMagnitude);
		}

		public string? GetDebugView() => _IsDebugOwner
			? FormatDebugView(SharedState)
			: null;

		private static double MapPulseToSignedOutput(double restPosition, double pulseMagnitude)
		{
			var clampedRest = Math.Clamp(restPosition, 0.0, 1.0);
			var clampedPulse = Math.Clamp(pulseMagnitude, 0.0, 1.0);
			var restSigned = clampedRest * 2.0 - 1.0;
			return restSigned + clampedPulse * (1.0 - restSigned);
		}
	}
}