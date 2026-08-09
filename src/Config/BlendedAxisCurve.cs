namespace SharpSticks.Config;

public sealed record BlendedAxisCurve :
	IAxisModifier,
	IMergeableObject<BlendedAxisCurve>
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
	// fades the integrated value back toward the normal curve; the fade is
	// one-way (folded into the latched state), so re-engaging holds the
	// output where it is instead of re-applying a previously faded offset.
	// Reaching the rest position resets the state so the normal curve fully
	// takes over again.
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
			public double LastFactorT;
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

				// Releasing fades the latched offset toward the normal curve in
				// proportion to the drop of the raw modifier factor. The fade is
				// folded into the state and only runs on a drop, so re-engaging
				// never re-applies a faded offset — pumping the modifier axis
				// with a steady input must not move the output. LastFactorT is
				// always above RestThreshold (the reset above), keeping the
				// division safe.
				if (factorT < state.LastFactorT)
				{
					state.LastOutput = normal + (state.LastOutput - normal) * (factorT / state.LastFactorT);
				}

				//TODO: optional movement-gated catch-up ("soft takeover"): also bleed
				// the latched offset proportionally to input travel, so a springless
				// axis re-anchors to the normal curve during long fully-engaged spells.
			}

			state.LastFactorT = factorT;

			// Clamp to the axis limits directly on the latched value —
			// otherwise the integrator winds up beyond what's representable
			// and the user has to "unwind" before the output moves again.
			state.LastOutput = Math.Clamp(state.LastOutput, -1.0, 1.0);
			return state.LastOutput;
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

	public BlendedAxisCurve Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = NormalCurve.MergeOrGet(context, ref hasChanged);
		var x2 = PrecisionCurve.MergeOrGet(context, ref hasChanged);
		var x3 = ModifierAxes.MergeOrGetAll(context, ref hasChanged, new()
		{
			ReturnUniqueItems = true,
		});

		return !hasChanged
			? this
			: this with
			{
				NormalCurve = x1,
				PrecisionCurve = x2,
				ModifierAxes = x3,
			};
	}
}