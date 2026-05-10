namespace ScaledAxisCSharp.Config;

public sealed record BlendedAxisCurve : IAxisModifier
{
	public required AxisCurve NormalCurve { get; init; }
	public required AxisCurve PrecisionCurve { get; init; }
	public required AxisBinding ModifierAxis { get; init; }
	public double FactorLow { get; init; }
	public double FactorHigh { get; init; } = 1.0;

	public IRuntimeAxisModifier CreateModifierRuntimeContext(IRuntimeContext context) =>
		new RuntimeModifier(this, context);

	public void FillDevices(ICollection<int> deviceIds)
	{
		deviceIds.Add(ModifierAxis.DeviceId);
	}

	private sealed record RuntimeModifier : IRuntimeAxisModifier
	{
		private readonly int _ModifierAxisDeviceIndex;
		private readonly JoystickDevice _ModifierAxisDevice;
		private readonly IRuntimeAxisModifier _NormalCurve;
		private readonly IRuntimeAxisModifier _PrecisionCurve;
		private readonly BlendedAxisCurve _Source;

		public RuntimeModifier(BlendedAxisCurve source, IRuntimeContext runtimeContext)
		{
			_Source = source;
			_NormalCurve = _Source.NormalCurve.CreateModifierRuntimeContext(runtimeContext);
			_PrecisionCurve = _Source.PrecisionCurve.CreateModifierRuntimeContext(runtimeContext);

			_ModifierAxisDevice = runtimeContext.DevicesById[_Source.ModifierAxis.DeviceId];
			_ModifierAxisDeviceIndex = runtimeContext.DeviceIndexesById[_Source.ModifierAxis.DeviceId];
		}

		public double Apply(double input, JoystickState?[] states)
		{
			if (ReadBlend(states) is not { } blend)
			{
				return input;
			}

			var result = _NormalCurve.Apply(input, states) * (1.0 - blend) +
			             _PrecisionCurve.Apply(input, states) * blend;

			return result;
		}

		private double? ReadBlend(JoystickState?[] states)
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

			return _Source.FactorLow + (_Source.FactorHigh - _Source.FactorLow) * factorT;
		}
	}
}