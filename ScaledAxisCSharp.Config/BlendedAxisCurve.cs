namespace ScaledAxisCSharp.Config;

public sealed record BlendedAxisCurve : IAxisModifier
{
	public double Apply(double input, IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
	{
		var blend = ReadBlend(states, devices);
		return NormalCurve.Apply(input) * (1.0 - blend) + PrecisionCurve.Apply(input) * blend;
	}

	private double ReadBlend(IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
	{
		var span = ModifierMax - ModifierMin;
		if (span == 0.0)
			return 0.0;

		if (!states.TryGetValue(ModifierAxis.DeviceId, out var state) ||
		    !devices.TryGetValue(ModifierAxis.DeviceId, out var device))
			return 0.0;

		var modifierValue = device.ReadAxisDebugSample(state, ModifierAxis).NormalizedValue;
		return Math.Clamp((modifierValue - ModifierMin) / span, 0.0, 1.0);
	}

	public required AxisCurve NormalCurve { get; init; }
	public required AxisCurve PrecisionCurve { get; init; }
	public required AxisBinding ModifierAxis { get; init; }
	public double ModifierMin { get; init; }
	public double ModifierMax { get; init; }

}