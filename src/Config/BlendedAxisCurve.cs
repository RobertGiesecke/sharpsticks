namespace SharpSticks.Config;

public sealed record BlendedAxisCurve : IAxisModifier
{
	public required IAxisModifier NormalCurve { get; init; }
	public required IAxisModifier PrecisionCurve { get; init; }
	public required AxisBinding ModifierAxis { get; init; }
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
		deviceIds.Add(ModifierAxis.DeviceId);
	}

	private sealed class RuntimeModifier<TInputDevice> : IRuntimeAxisModifier
		where TInputDevice : JoystickDevice
	{
		private readonly int _ModifierAxisDeviceIndex;
		private readonly TInputDevice _ModifierAxisDevice;
		private readonly IRuntimeAxisModifier _NormalCurve;
		private readonly IRuntimeAxisModifier _PrecisionCurve;
		private readonly BlendedAxisCurve _Source;

		private bool _HasState;
		private double _LastInput;
		private double _LastOutput;

		public RuntimeModifier(BlendedAxisCurve source, IRuntimeContext<TInputDevice> runtimeContext)
		{
			_Source = source;
			_NormalCurve = _Source.NormalCurve.CreateModifierRuntimeContext(runtimeContext);
			_PrecisionCurve = _Source.PrecisionCurve.CreateModifierRuntimeContext(runtimeContext);

			_ModifierAxisDevice = runtimeContext.DevicesById[_Source.ModifierAxis.DeviceId];
			_ModifierAxisDeviceIndex = runtimeContext.DeviceIndexesById[_Source.ModifierAxis.DeviceId];
		}

		public double Apply(double input, JoystickState?[] states)
		{
			if (ReadBlend(states) is not { } readout)
			{
				return input;
			}

			var (blend, factorT) = readout;
			var normal = _NormalCurve.Apply(input, states);
			var blended = normal * (1.0 - blend) + _PrecisionCurve.Apply(input, states) * blend;

			if (!_Source.Stateful)
			{
				return blended;
			}

			// Modifier at rest fully restores the normal curve and clears any
			// latched state so the next engagement starts fresh.
			if (factorT <= _Source.RestThreshold)
			{
				_HasState = false;
				return normal;
			}

			if (!_HasState)
			{
				_LastInput = input;
				_LastOutput = normal;
				_HasState = true;
			}
			else
			{
				// Integrate: only the input delta under the current blended
				// curve moves the latched value.
				_LastOutput += blended - BlendAt(_LastInput, blend, states);
				_LastInput = input;
			}

			// Lerp on the raw modifier factor so releasing the axis fades the
			// integrated value back toward the normal curve.
			var output = normal * (1.0 - factorT) + _LastOutput * factorT;

			// Clamp to the axis limits and bleed any excess off the latched
			// value — otherwise the integrator winds up beyond what's
			// representable and the user has to "unwind" before the output
			// moves again.
			const double max = 1.0;
			const double min = -1.0;
			if (output > max)
			{
				_LastOutput = (max - normal * (1.0 - factorT)) / factorT;
				return max;
			}

			if (output < min)
			{
				_LastOutput = (min - normal * (1.0 - factorT)) / factorT;
				return min;
			}

			return output;
		}

		private double BlendAt(double input, double blend, JoystickState?[] states) =>
			_NormalCurve.Apply(input, states) * (1.0 - blend) +
			_PrecisionCurve.Apply(input, states) * blend;

		private (double Blend, double FactorT)? ReadBlend(JoystickState?[] states)
		{
			if (states[_ModifierAxisDeviceIndex] is not { } modifierState)
			{
				return null;
			}

			var modifierValue = _ModifierAxisDevice.ReadAxisDebugSample(modifierState, _Source.ModifierAxis)
				.NormalizedValue;
			var factorT = _Source.ModifierAxis.Mode == AxisMode.Signed
				? Math.Clamp((modifierValue + 1.0) * 0.5, 0.0, 1.0)
				: Math.Clamp(modifierValue, 0.0, 1.0);

			var blend = _Source.FactorLow + (_Source.FactorHigh - _Source.FactorLow) * factorT;
			return (blend, factorT);
		}
	}
}