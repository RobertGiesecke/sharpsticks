using System.Globalization;
using System.Text;

namespace SharpSticks.InputAbstractions;

internal sealed record AbsoluteRelativeAxisModifier :
	IAxisModifier,
	IMergeableObject<AbsoluteRelativeAxisModifier>
{
	// Normalized target within this of a rail counts as "at the edge" for the
	// edge-hold; clamped mapping yields exact 0/1 at/beyond the source extremes.
	private const double EdgeEpsilon = 1e-9;

	private SharedStateClass SharedState { get; init; }

	// null = bidirectional: both directions on one output axis, resting at
	// center, increase pulsing positive and decrease negative.
	private readonly RelativeDirection? _Direction;
	private readonly double _RestPosition;

	public static (AbsoluteRelativeAxisModifier Increase, AbsoluteRelativeAxisModifier Decrease) Create(
		AbsoluteRelativeAxisOptions options)
	{
		var sharedState = new SharedStateClass(options);
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

	private AbsoluteRelativeAxisModifier(SharedStateClass sharedState, RelativeDirection? direction,
		double restPosition)
	{
		SharedState = sharedState;
		_Direction = direction;
		_RestPosition = Math.Clamp(restPosition, 0.0, 1.0);
	}

	void IFillDevices.FillDevices(ICollection<int> deviceIds)
	{
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		_Direction is { } direction
			? new DualAxesRuntimeModifier(SharedState, direction, _RestPosition, context.TimeSource)
			: new BidirectionalRuntimeModifier(SharedState, context.TimeSource);

	private enum RelativeDirection
	{
		Increase,
		Decrease,
	}

	private sealed class SharedStateClass : IMergeableObject<SharedStateClass>
	{
		private readonly AbsoluteRelativeAxisOptions _Options;
		private readonly double _Minimum;
		private readonly double _Maximum;
		private double _Velocity;

		public SharedStateClass(AbsoluteRelativeAxisOptions options)
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

		/// <summary>The target, normalized to [0,1] across the range — 1 at the
		/// top rail, 0 at the bottom. Used to detect the edge-hold extremes.</summary>
		public double NormalizedTargetFor(double input) => NormalizeTarget(MapInputToTarget(input));

		public double GetDesiredPulseMagnitude(double input, RelativeDirection direction, double signedFeedForward = 0.0)
		{
			var target = MapInputToTarget(input);
			var error = target - Current;
			LastSourceInput = input;
			LastTarget = target;
			LastError = error;
			LastCurrentBefore = Current;
			LastCurrentAfter = Current;
			// Deadband suppresses only the proportional correction. A moving target
			// must retain velocity feed-forward even when each small frame-to-frame
			// increment is inside ErrorTolerance, otherwise the internal position
			// advances while the real consumer receives no pulse.
			var correction = Math.Abs(error) > _Options.ErrorTolerance ? error * _Options.Gain : 0.0;
			var signedOutput = correction + signedFeedForward;
			if (Math.Abs(signedOutput) <= 1e-12)
			{
				LastActiveDirection = null;
				SetDesired(direction, 0.0);
				return 0.0;
			}

			var isIncrease = signedOutput > 0.0;
			LastActiveDirection = isIncrease ? RelativeDirection.Increase : RelativeDirection.Decrease;
			if ((direction == RelativeDirection.Increase) != isIncrease)
			{
				SetDesired(direction, 0.0);
				return 0.0;
			}

			// A purely proportional controller must wait for a position error to
			// exist, so it always trails a smoothly moving absolute source. The
			// optional feed-forward term sends the pulse implied by target velocity
			// immediately; the error term keeps it calibrated to the real consumer.
			var outputMagnitude = Math.Min(Math.Abs(signedOutput), Math.Abs(_Options.MaxOutput));
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
		public void AdvanceNet(double input, double netPulse, double elapsedSeconds)
		{
			SetActual(RelativeDirection.Increase, Math.Max(netPulse, 0.0));
			SetActual(RelativeDirection.Decrease, Math.Max(-netPulse, 0.0));
			var target = MapInputToTarget(input);
			var error = target - Current;
			if (HasDynamicResponse())
			{
				AdvanceDynamic(netPulse, elapsedSeconds);
				if (netPulse == 0.0 && Math.Abs(_Velocity) < 1e-6 && Math.Abs(error) <= _Options.ErrorTolerance)
					Current = target;
				LastCurrentAfter = Current;
				return;
			}

			if (Math.Abs(error) <= _Options.ErrorTolerance)
			{
				Current = target;
				LastCurrentAfter = Current;
				return;
			}

			var direction = netPulse > 0.0 ? RelativeDirection.Increase : RelativeDirection.Decrease;
			var unitsPerSecond = UnitsPerSecond(direction);
			if (netPulse == 0.0 || unitsPerSecond <= 0.0)
			{
				LastCurrentAfter = Current;
				return;
			}

			var step = Math.Sign(netPulse) * VelocityFraction(direction, Math.Abs(netPulse)) *
				unitsPerSecond * elapsedSeconds;

			// Moving toward the target must not overshoot it; moving away from it
			// integrates as-is because the consumer moved too.
			if (Math.Sign(step) == Math.Sign(error) && Math.Abs(step) > Math.Abs(error))
			{
				step = error;
			}

			Current = Clamp(Current + step);
			LastCurrentAfter = Current;
		}

		public void Advance(double input, RelativeDirection direction, double actualPulseMagnitude, double elapsedSeconds)
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

			var unitsPerSecond = UnitsPerSecond(direction);
			if (unitsPerSecond <= 0.0 || actualPulseMagnitude <= 0.0)
			{
				LastCurrentAfter = Current;
				return;
			}

			var step = VelocityFraction(direction, actualPulseMagnitude) * unitsPerSecond * elapsedSeconds;
			if (step > Math.Abs(error)) step = Math.Abs(error);
			Current = Clamp(Current + (isIncrease ? step : -step));
			LastCurrentAfter = Current;
		}

		private bool HasDynamicResponse() =>
			_Options.IncreaseResponseTimeConstant > TimeSpan.Zero ||
			_Options.DecreaseResponseTimeConstant > TimeSpan.Zero;

		private void AdvanceDynamic(double netPulse, double elapsedSeconds)
		{
			var pulseDirection = netPulse > 0.0 ? RelativeDirection.Increase : RelativeDirection.Decrease;
			var desiredVelocity = netPulse == 0.0
				? 0.0
				: Math.Sign(netPulse) * VelocityFraction(pulseDirection, Math.Abs(netPulse)) *
					UnitsPerSecond(pulseDirection);
			var responseDirection = desiredVelocity != 0.0
				? (desiredVelocity > 0.0 ? RelativeDirection.Increase : RelativeDirection.Decrease)
				: (_Velocity >= 0.0 ? RelativeDirection.Increase : RelativeDirection.Decrease);
			var tau = (responseDirection == RelativeDirection.Increase
				? _Options.IncreaseResponseTimeConstant
				: _Options.DecreaseResponseTimeConstant).TotalSeconds;
			if (tau <= 0.0)
			{
				_Velocity = desiredVelocity;
				Current = Clamp(Current + _Velocity * elapsedSeconds);
				return;
			}

			var decay = Math.Exp(-Math.Max(elapsedSeconds, 0.0) / tau);
			var displacement = desiredVelocity * elapsedSeconds +
				(_Velocity - desiredVelocity) * tau * (1.0 - decay);
			var unclamped = Current + displacement;
			_Velocity = desiredVelocity + (_Velocity - desiredVelocity) * decay;
			Current = Clamp(unclamped);
			if ((Current <= _Minimum && _Velocity < 0.0) || (Current >= _Maximum && _Velocity > 0.0))
				_Velocity = 0.0;
		}

		public double GetPulseForTargetVelocity(double targetVelocity)
		{
			if (Math.Abs(targetVelocity) <= Math.Abs(_Options.TargetVelocityDeadband) ||
				_Options.TargetVelocityFeedForward <= 0.0)
			{
				return 0.0;
			}

			var direction = targetVelocity > 0.0 ? RelativeDirection.Increase : RelativeDirection.Decrease;
			var unitsPerSecond = UnitsPerSecond(direction);
			if (unitsPerSecond <= 0.0) return 0.0;
			var desiredFraction = Math.Abs(targetVelocity) / unitsPerSecond * _Options.TargetVelocityFeedForward;
			return Math.CopySign(PulseForVelocityFraction(direction, desiredFraction), targetVelocity);
		}

		public double TargetForInput(double input) => MapInputToTarget(input);

		public double InputForTarget(double target)
		{
			if (_Maximum <= _Minimum) return _Options.SourceInputMinimum;
			var normalized = Math.Clamp((target - _Minimum) / (_Maximum - _Minimum), 0.0, 1.0);
			return _Options.SourceInputMinimum +
				normalized * (_Options.SourceInputMaximum - _Options.SourceInputMinimum);
		}

		// Range-per-second the model advances at full pulse for a direction.
		// A zero or negative TimeToFull freezes the model (0).
		private double UnitsPerSecond(RelativeDirection direction)
		{
			var secondsToFull = (direction == RelativeDirection.Increase
				? _Options.IncreaseTimeToFull
				: _Options.DecreaseTimeToFull).TotalSeconds;
			return secondsToFull > 0.0
				? (_Maximum - _Minimum) / secondsToFull
				: 0.0;
		}

		private double VelocityFraction(RelativeDirection direction, double pulse)
		{
			var deadzone = ResponseDeadzone(direction);
			if (pulse <= deadzone) return 0.0;
			var exponent = ResponseExponent(direction);
			var normalized = Math.Clamp((pulse - deadzone) / Math.Max(1.0 - deadzone, 1e-9), 0.0, 1.0);
			return Math.Pow(normalized, exponent);
		}

		private double PulseForVelocityFraction(RelativeDirection direction, double fraction)
		{
			if (fraction <= 0.0) return 0.0;
			var deadzone = ResponseDeadzone(direction);
			var exponent = ResponseExponent(direction);
			var normalizedPulse = Math.Pow(Math.Clamp(fraction, 0.0, 1.0), 1.0 / exponent);
			return deadzone + normalizedPulse * (1.0 - deadzone);
		}

		private double ResponseDeadzone(RelativeDirection direction) => Math.Clamp(
			direction == RelativeDirection.Increase
				? _Options.IncreaseResponseDeadzone
				: _Options.DecreaseResponseDeadzone,
			0.0, 0.999999);

		private double ResponseExponent(RelativeDirection direction)
		{
			var exponent = direction == RelativeDirection.Increase
				? _Options.IncreaseResponseExponent
				: _Options.DecreaseResponseExponent;
			return double.IsFinite(exponent) && exponent > 0.0 ? exponent : 1.0;
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
		public SharedStateClass Merge(MergeObjectContext context) => this;
	}

	private abstract record RuntimeModifier<TState> : StatefulRuntimeInputModifier<double, TState>
		where TState : struct
	{
		protected SharedStateClass SharedState { get; }
		protected ITimeSource TimeSource { get; }
		private readonly long _StartTimestamp;

		protected RuntimeModifier(SharedStateClass sharedState, ITimeSource timeSource)
		{
			SharedState = sharedState;
			TimeSource = timeSource;
			// First frame measures from construction, not from 0, so it
			// doesn't integrate a huge gap.
			_StartTimestamp = timeSource.GetTimestamp();
		}

		// Seconds since the previous Update frame (or construction, the first
		// time). The caller threads the per-instance last timestamp through its
		// state struct so peeks — which run on a copy — never disturb it.
		protected double ElapsedSeconds(bool hasLast, long lastTimestamp, long now) =>
			(now - (hasLast ? lastTimestamp : _StartTimestamp)) / (double)TimeSource.Frequency;

		// Slew the emitted pulse toward its desired value, capped to what the
		// ramp-time budget allows this frame. The pulse magnitude spans 0→1, so
		// secondsToFull == 1 / maxStepPerSecond; a non-positive ramp time means
		// instant (no smoothing). Wall-clock based, so frame rate doesn't matter.
		protected static double Slew(
			double current, double desired, double riseSeconds, double fallSeconds, double elapsedSeconds)
		{
			var seconds = desired > current ? riseSeconds : fallSeconds;
			var maxStep = seconds > 0.0 ? elapsedSeconds / seconds : double.PositiveInfinity;
			return MoveTowards(current, desired, maxStep);
		}

		private static double MoveTowards(double current, double desired, double maxStep)
		{
			// No budget this frame (no time elapsed) → hold. +Infinity (instant
			// / no smoothing) sails past the abs-delta check below to snap.
			if (maxStep <= 0.0)
			{
				return current;
			}

			var delta = desired - current;
			if (Math.Abs(delta) <= maxStep)
			{
				return desired;
			}

			return current + Math.CopySign(maxStep, delta);
		}

		protected static NumberFormattingDebugInterpolatedStringHandler FormatDebugView(SharedStateClass sharedState) =>
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
			$"desired+={sharedState.LastDesiredIncrease} " +
			$"actual+={sharedState.LastActualIncrease} " +
			$"desired-={sharedState.LastDesiredDecrease} " +
			$"actual-={sharedState.LastActualDecrease}";
	}

	private sealed record BidirectionalRuntimeModifier :
		RuntimeModifier<BidirectionalRuntimeModifier.PulseState>,
		IRuntimeAxisModifier,
		IRuntimeAxisDebugView,
		IContinuousRuntimeModifier
	{
		internal struct PulseState
		{
			public double IncreasePulse;
			public double DecreasePulse;
			public double IncreaseEdgeHeldSeconds;
			public double DecreaseEdgeHeldSeconds;
			public long LastTimestamp;
			public bool HasTimestamp;
			public double LastTarget;
			public bool HasTarget;
			public double SmoothedTargetVelocity;
			public bool HasSmoothedTargetVelocity;
			public double SmoothedTarget;
			public bool HasSmoothedTarget;
			public int LastTargetDirection;
			public double ReversalBoostRemainingSeconds;
		}

		private readonly double _OutputRiseSeconds;
		private readonly double _OutputFallSeconds;
		private readonly double _MaxOutput;
		private readonly double _IncreaseEdgeHoldSeconds;
		private readonly double _DecreaseEdgeHoldSeconds;

		public BidirectionalRuntimeModifier(SharedStateClass sharedState, ITimeSource timeSource)
			: base(sharedState, timeSource)
		{
			var options = sharedState.Options;
			_OutputRiseSeconds = options.OutputRiseTime.TotalSeconds;
			_OutputFallSeconds = options.OutputFallTime.TotalSeconds;
			_MaxOutput = Math.Abs(options.MaxOutput);
			_IncreaseEdgeHoldSeconds = options.IncreaseEdgeHoldTime.TotalSeconds;
			_DecreaseEdgeHoldSeconds = options.DecreaseEdgeHoldTime.TotalSeconds;
		}

		protected override double Apply(double input, JoystickState?[] states, ref PulseState state, ApplyMode mode)
		{
			// Both directions evaluate against the one SharedState; only the
			// direction matching the error sign produces a non-zero desired
			// pulse, the other decays at the fall rate. SharedState sits
			// outside the state struct, so it is only advanced on real frames.
			var now = TimeSource.GetTimestamp();
			var elapsedSeconds = ElapsedSeconds(state.HasTimestamp, state.LastTimestamp, now);
			var rawTarget = SharedState.TargetForInput(input);
			var target = rawTarget;
			var targetSmoothingSeconds =
				SharedState.Options.TargetPositionSmoothingTimeConstant.TotalSeconds;
			if (state.HasSmoothedTarget && elapsedSeconds > 0.0 && targetSmoothingSeconds > 0.0)
			{
				var decay = Math.Exp(-elapsedSeconds / targetSmoothingSeconds);
				target = rawTarget + (state.SmoothedTarget - rawTarget) * decay;
			}
			if (mode == ApplyMode.Update)
			{
				state.SmoothedTarget = target;
				state.HasSmoothedTarget = true;
			}
			var controlInput = SharedState.InputForTarget(target);
			var rawTargetVelocity = state.HasTarget && elapsedSeconds > 0.0
				? (target - state.LastTarget) / elapsedSeconds
				: 0.0;
			var targetVelocity = rawTargetVelocity;
			var velocitySmoothingSeconds =
				SharedState.Options.TargetVelocitySmoothingTimeConstant.TotalSeconds;
			if (state.HasTarget && elapsedSeconds > 0.0 && velocitySmoothingSeconds > 0.0)
			{
				var previous = state.HasSmoothedTargetVelocity ? state.SmoothedTargetVelocity : 0.0;
				var decay = Math.Exp(-elapsedSeconds / velocitySmoothingSeconds);
				targetVelocity = rawTargetVelocity + (previous - rawTargetVelocity) * decay;
			}
			if (mode == ApplyMode.Update)
			{
				state.SmoothedTargetVelocity = targetVelocity;
				state.HasSmoothedTargetVelocity = true;
			}
			var velocityFeedForward = SharedState.GetPulseForTargetVelocity(targetVelocity);
			var desiredIncrease = SharedState.GetDesiredPulseMagnitude(controlInput, RelativeDirection.Increase,
				velocityFeedForward);
			var desiredDecrease = SharedState.GetDesiredPulseMagnitude(controlInput, RelativeDirection.Decrease,
				velocityFeedForward);

			// Edge hold: while the input is pinned at a rail, force full drive
			// for the configured time so the consumer is slammed to that rail
			// no matter what the model believes.
			var normalizedTarget = SharedState.NormalizedTargetFor(input);
			var atTop = normalizedTarget >= 1.0 - EdgeEpsilon;
			var atBottom = normalizedTarget <= EdgeEpsilon;
			if (atTop && state.IncreaseEdgeHeldSeconds < _IncreaseEdgeHoldSeconds)
			{
				desiredIncrease = _MaxOutput;
			}

			if (atBottom && state.DecreaseEdgeHeldSeconds < _DecreaseEdgeHoldSeconds)
			{
				desiredDecrease = _MaxOutput;
			}

			if (SharedState.Options.SuppressOpposingPulseUntilSourceReverses)
			{
				var velocityEpsilon = Math.Max(0.002,
					Math.Abs(SharedState.Options.TargetVelocityDeadband));
				var targetDirection = Math.Abs(targetVelocity) > velocityEpsilon
					? Math.Sign(targetVelocity)
					: state.LastTargetDirection;
				if (mode == ApplyMode.Update && Math.Abs(targetVelocity) > velocityEpsilon)
				{
					if (state.LastTargetDirection != 0 && targetDirection != state.LastTargetDirection)
						state.ReversalBoostRemainingSeconds =
							SharedState.Options.DirectionReversalBoostTime.TotalSeconds;
					state.LastTargetDirection = targetDirection;
				}
				if (state.ReversalBoostRemainingSeconds > 0.0)
				{
					if (targetDirection > 0) desiredIncrease = _MaxOutput;
					else if (targetDirection < 0) desiredDecrease = _MaxOutput;
					if (mode == ApplyMode.Update)
						state.ReversalBoostRemainingSeconds = Math.Max(0.0,
							state.ReversalBoostRemainingSeconds - elapsedSeconds);
				}
				if (targetDirection > 0) desiredDecrease = 0.0;
				else if (targetDirection < 0) desiredIncrease = 0.0;
			}

			state.IncreasePulse =
				Slew(state.IncreasePulse, desiredIncrease, _OutputRiseSeconds, _OutputFallSeconds, elapsedSeconds);
			state.DecreasePulse =
				Slew(state.DecreasePulse, desiredDecrease, _OutputRiseSeconds, _OutputFallSeconds, elapsedSeconds);
			if (SharedState.Options.SuppressOpposingPulseUntilSourceReverses)
			{
				if (state.LastTargetDirection > 0) state.DecreasePulse = 0.0;
				else if (state.LastTargetDirection < 0) state.IncreasePulse = 0.0;
			}

			// Rest is center: increase pulses push positive, decrease negative.
			// The consumer only ever sees this NET deflection — during a
			// direction flip the decaying opposite pulse cancels part of the
			// rising one — so the model must integrate the net as well, or it
			// races ahead of what the consumer actually received.
			var net = Math.Clamp(state.IncreasePulse, 0.0, 1.0) - Math.Clamp(state.DecreasePulse, 0.0, 1.0);
			if (mode == ApplyMode.Update)
			{
				state.IncreaseEdgeHeldSeconds = atTop ? state.IncreaseEdgeHeldSeconds + elapsedSeconds : 0.0;
				state.DecreaseEdgeHeldSeconds = atBottom ? state.DecreaseEdgeHeldSeconds + elapsedSeconds : 0.0;
				SharedState.AdvanceNet(controlInput, net, elapsedSeconds);
				state.LastTimestamp = now;
				state.HasTimestamp = true;
				state.LastTarget = target;
				state.HasTarget = true;
			}

			return net;
		}

		public NumberFormattingDebugInterpolatedStringHandler GetDebugView() => FormatDebugView(SharedState);
	}

	private sealed record DualAxesRuntimeModifier :
		RuntimeModifier<DualAxesRuntimeModifier.PulseState>,
		IRuntimeAxisModifier,
		IRuntimeAxisDebugView,
		IContinuousRuntimeModifier
	{
		internal struct PulseState
		{
			public double CurrentPulseMagnitude;
			public double EdgeHeldSeconds;
			public long LastTimestamp;
			public bool HasTimestamp;
		}

		private readonly RelativeDirection _Direction;
		private readonly double _RestPosition;
		private readonly double _OutputRiseSeconds;
		private readonly double _OutputFallSeconds;
		private readonly double _MaxOutput;
		private readonly double _EdgeHoldSeconds;
		private readonly bool _IsDebugOwner;

		public DualAxesRuntimeModifier(
			SharedStateClass sharedState, RelativeDirection direction, double restPosition, ITimeSource timeSource)
			: base(sharedState, timeSource)
		{
			var options = sharedState.Options;
			_Direction = direction;
			_RestPosition = restPosition;
			_OutputRiseSeconds = options.OutputRiseTime.TotalSeconds;
			_OutputFallSeconds = options.OutputFallTime.TotalSeconds;
			_MaxOutput = Math.Abs(options.MaxOutput);
			_EdgeHoldSeconds = (direction == RelativeDirection.Increase
				? options.IncreaseEdgeHoldTime
				: options.DecreaseEdgeHoldTime).TotalSeconds;
			_IsDebugOwner = direction == RelativeDirection.Decrease;
		}

		protected override double Apply(double input, JoystickState?[] states, ref PulseState state, ApplyMode mode)
		{
			// GetDesiredPulseMagnitude only records debug-view fields. The
			// per-instance pulse magnitude lives in the state struct (peeks
			// run on a copy); SharedState is shared with the paired direction
			// and sits outside the struct, so it is only advanced on real
			// frames.
			var now = TimeSource.GetTimestamp();
			var elapsedSeconds = ElapsedSeconds(state.HasTimestamp, state.LastTimestamp, now);
			var desiredPulseMagnitude = SharedState.GetDesiredPulseMagnitude(input, _Direction);

			// Edge hold: this direction's own rail (top for Increase, bottom for
			// Decrease) — force full drive there for the configured time.
			var normalizedTarget = SharedState.NormalizedTargetFor(input);
			var atEdge = _Direction == RelativeDirection.Increase
				? normalizedTarget >= 1.0 - EdgeEpsilon
				: normalizedTarget <= EdgeEpsilon;
			if (atEdge && state.EdgeHeldSeconds < _EdgeHoldSeconds)
			{
				desiredPulseMagnitude = _MaxOutput;
			}

			state.CurrentPulseMagnitude = Slew(state.CurrentPulseMagnitude, desiredPulseMagnitude,
				_OutputRiseSeconds, _OutputFallSeconds, elapsedSeconds);
			if (mode == ApplyMode.Update)
			{
				state.EdgeHeldSeconds = atEdge ? state.EdgeHeldSeconds + elapsedSeconds : 0.0;
				SharedState.Advance(input, _Direction, state.CurrentPulseMagnitude, elapsedSeconds);
				state.LastTimestamp = now;
				state.HasTimestamp = true;
			}

			return MapPulseToSignedOutput(_RestPosition, state.CurrentPulseMagnitude);
		}

		public NumberFormattingDebugInterpolatedStringHandler GetDebugView() => _IsDebugOwner
			? FormatDebugView(SharedState)
			: NumberFormattingDebugInterpolatedStringHandler.Empty();

		private static double MapPulseToSignedOutput(double restPosition, double pulseMagnitude)
		{
			var clampedRest = Math.Clamp(restPosition, 0.0, 1.0);
			var clampedPulse = Math.Clamp(pulseMagnitude, 0.0, 1.0);
			var restSigned = clampedRest * 2.0 - 1.0;
			return restSigned + clampedPulse * (1.0 - restSigned);
		}
	}

	public AbsoluteRelativeAxisModifier Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = SharedState.MergeOrGet(context, ref hasChanged);

		return !hasChanged
			? this
			: this with
			{
				SharedState = x1,
			};
	}
}
