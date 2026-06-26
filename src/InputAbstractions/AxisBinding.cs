namespace SharpSticks.InputAbstractions;

/// <summary>
/// Identifies an input axis plus how to normalize it. As an
/// <see cref="IAxisModifier"/>, a binding ignores the incoming value and
/// yields the bound axis's current normalized value (per <see cref="Mode"/>) —
/// the building block for modifiers that are driven by another axis, e.g.
/// <c>BlendedAxisCurve.ModifierAxis</c>. Yields 0 while the device state is
/// unavailable.
/// </summary>
public sealed record AxisBinding(
	int DeviceId,
	Axis Axis,
	AxisMode Mode = AxisMode.Signed,
	bool Invert = false,
	double Deadzone = 0.0) : InputBinding(DeviceId),
	IAxisModifier,
	IMergeableObject<AxisBinding>
{
	public void FillDevices(ICollection<int> deviceIds) => deviceIds.Add(DeviceId);

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new AxisValueReader<TInputDevice>(
			context.DevicesById[DeviceId],
			context.DeviceIndexesById[DeviceId],
			this);

	private sealed class AxisValueReader<TInputDevice>(
		TInputDevice device,
		int deviceIndex,
		AxisBinding binding) : IRuntimeAxisModifier
		where TInputDevice : JoystickDevice
	{
		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update) =>
			states[deviceIndex] is { } state
				? device.ReadNormalizedAxisValue(state, binding)
				: 0.0;
	}

	public AxisBinding Merge(MergeObjectContext context) => this;
}