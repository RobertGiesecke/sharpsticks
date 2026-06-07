namespace SharpSticks.Config;

public sealed record BlendedAxisCurve : IAxisModifier
{
	public required IAxisModifier NormalCurve { get; init; }
	public required IAxisModifier PrecisionCurve { get; init; }

	/// <summary>
	/// Produces the blend factor each frame; the result is clamped to [0, 1]
	/// (0 = <see cref="NormalCurve"/>, 1 = fully engaged). Typically an
	/// <see cref="AxisBinding"/> — bind a lever that rests at the hardware
	/// minimum with <see cref="AxisMode.Unsigned"/> so rest reads 0 and fully
	/// engaged reads 1; a signed binding's negative half clamps to 0.
	/// </summary>
	public required ImmutableArray<IAxisModifier> ModifierAxes { get; init; }

	public double FactorLow { get; init; }
	public double FactorHigh { get; init; } = 1.0;

	// When true, the output is integrated from input deltas through the
	// currently-active blended curve, so engaging or moving the modifier
	// axis no longer makes the output jump. Releasing the modifier axis
	// fades the integrated value back toward the normal curve; reaching
	// the rest position resets the state so the normal curve fully takes
	// over again.
	public bool Stateful { get; init; }

	// Modifier-axis values whose normalized magnitude is at or below this
	// threshold are treated as fully at rest — the integrated state is
	// cleared and the output snaps to the normal curve. Tune up for noisy
	// axes that never quite reach 0.
	public double RestThreshold { get; init; } = 1e-3;

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new RuntimeModifier<TInputDevice>(this, context);

	public void FillDevices(ICollection<int> deviceIds)
	{
		foreach (var axisModifier in ModifierAxes)
		{
			axisModifier.FillDevices(deviceIds);
		}
	}

	private sealed record RuntimeModifier<TInputDevice> :
		StatefulRuntimeInputModifier<double, RuntimeModifier<TInputDevice>.LatchState>,
		IRuntimeAxisModifier
		where TInputDevice : JoystickDevice
	{
		internal struct LatchState
		{
			public bool HasState;
			public double LastInput;
			public double LastOutput;
		}

		private readonly ImmutableArray<IRuntimeAxisModifier> _ModifierAxisModifiers;
		private readonly IRuntimeAxisModifier _NormalCurve;
		private readonly IRuntimeAxisModifier _PrecisionCurve;
		private readonly BlendedAxisCurve _Source;

		public RuntimeModifier(BlendedAxisCurve source, IRuntimeContext<TInputDevice> runtimeContext)
		{
			_Source = source;
			_NormalCurve = _Source.NormalCurve.CreateModifierRuntimeContext(runtimeContext);
			_PrecisionCurve = _Source.PrecisionCurve.CreateModifierRuntimeContext(runtimeContext);
			using var modifiers = new PooledList<IRuntimeAxisModifier>(source.ModifierAxes.Length);
			foreach (var axisModifier in source.ModifierAxes)
			{
				modifiers.Add(axisModifier.CreateModifierRuntimeContext(runtimeContext));
			}

			_ModifierAxisModifiers = [..modifiers.Span];
		}

		protected override double Apply(double input, JoystickState?[] states, ref LatchState state, ApplyMode mode)
		{
			var (blend, factorT) = ReadBlend(states, mode);
			var normal = _NormalCurve.Apply(input, states, mode);
			var blended = normal * (1.0 - blend) + _PrecisionCurve.Apply(input, states, mode) * blend;

			if (!_Source.Stateful)
			{
				return blended;
			}

			// Modifier at rest fully restores the normal curve and clears any
			// latched state so the next engagement starts fresh.
			if (factorT <= _Source.RestThreshold)
			{
				state.HasState = false;
				return normal;
			}

			if (!state.HasState)
			{
				state.LastInput = input;
				state.LastOutput = normal;
				state.HasState = true;
			}
			else
			{
				// Integrate: only the input delta under the current blended
				// curve moves the latched value.
				state.LastOutput += blended - BlendAt(state.LastInput, blend, states);
				state.LastInput = input;
			}

			// Lerp on the raw modifier factor so releasing the axis fades the
			// integrated value back toward the normal curve.
			var output = normal * (1.0 - factorT) + state.LastOutput * factorT;

			// Clamp to the axis limits and bleed any excess off the latched
			// value — otherwise the integrator winds up beyond what's
			// representable and the user has to "unwind" before the output
			// moves again.
			const double max = 1.0;
			const double min = -1.0;
			if (output > max)
			{
				state.LastOutput = (max - normal * (1.0 - factorT)) / factorT;
				return max;
			}

			if (output < min)
			{
				state.LastOutput = (min - normal * (1.0 - factorT)) / factorT;
				return min;
			}

			return output;
		}

		// Probes the curves at a hypothetical (previous) input — always a peek:
		// the regular per-frame Update call for the child curves already
		// happened at the current input above.
		private double BlendAt(double input, double blend, JoystickState?[] states) =>
			_NormalCurve.Apply(input, states, ApplyMode.Peek) * (1.0 - blend) +
			_PrecisionCurve.Apply(input, states, ApplyMode.Peek) * blend;

		// The modifier source ignores its input (an AxisBinding reads the
		// bound axis; see AxisBinding's IAxisModifier implementation). A
		// missing device reads 0 → fully at rest → the normal curve. The
		// outer mode is forwarded — this is the source's one regular
		// evaluation per frame, and it may be stateful (e.g. smoothed).
		private (double Blend, double FactorT) ReadBlend(JoystickState?[] states, ApplyMode mode)
		{
			// Max across all sources: whichever lever is engaged furthest
			// drives the blend. Every source still gets its one regular
			// evaluation per frame (no early exit) so stateful sources keep
			// advancing. Seeding at 0 doubles as the lower clamp and as the
			// rest value for an empty array.
			var maxValue = 0.0;
			foreach (var axisModifier in _ModifierAxisModifiers)
			{
				maxValue = Math.Max(maxValue, axisModifier.Apply(0.0, states, mode));
			}

			var factorT = Math.Min(maxValue, 1.0);
			var blend = _Source.FactorLow + (_Source.FactorHigh - _Source.FactorLow) * factorT;
			return (blend, factorT);
		}
	}
}