namespace ScaledAxisCSharp.Config;

public sealed record BlendedAxisCurve : IAxisModifier
{
	public required AxisCurve NormalCurve { get; init; }
	public required AxisCurve PrecisionCurve { get; init; }
	public required AxisBinding ModifierAxis { get; init; }
	public double FactorLow { get; init; }
	public double FactorHigh { get; init; } = 1.0;

	public double Apply(double input, IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
	{
		if (ReadBlend(states, devices) is not { } blend)
		{
			return input;
		}

		var result = NormalCurve.Apply(input) * (1.0 - blend) + PrecisionCurve.Apply(input) * blend;
		
		return result;
	}

	private double? ReadBlend(IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
	{
		if (!states.TryGetValue(ModifierAxis.DeviceId, out var modifierState) ||
		    !devices.TryGetValue(ModifierAxis.DeviceId, out var modifierDevice))
		{
			return null;
		}

		var modifierValue = modifierDevice.ReadAxisDebugSample(modifierState, ModifierAxis).NormalizedValue;
		var factorT = ModifierAxis.Mode == AxisMode.Signed
			? Math.Clamp((modifierValue + 1.0) * 0.5, 0.0, 1.0)
			: Math.Clamp(modifierValue, 0.0, 1.0);

		return FactorLow + (FactorHigh - FactorLow) * factorT;
	}
}
