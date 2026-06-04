namespace SharpSticks.InputAbstractions;

internal sealed class MergeAxesModifier : IAxisModifier
{
	private readonly AxisBinding _Second;
	private readonly double _SecondScale;
	private readonly double _SecondOffset;
	private readonly IAxisModifier? _SecondModifier;
	private readonly IAxisModifier? _FirstModifier;
	private readonly MergeMode _Mode;

	public MergeAxesModifier(
		AxisBinding second,
		double secondScale,
		double secondOffset,
		IAxisModifier? secondModifier,
		IAxisModifier? firstModifier,
		MergeMode mode)
	{
		_Second = second;
		_SecondScale = secondScale;
		_SecondOffset = secondOffset;
		_SecondModifier = secondModifier;
		_FirstModifier = firstModifier;
		_Mode = mode;
	}

	public void FillDevices(ICollection<int> deviceIds)
	{
		deviceIds.Add(_Second.DeviceId);
		_SecondModifier?.FillDevices(deviceIds);
		_FirstModifier?.FillDevices(deviceIds);
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new RuntimeModifier<TInputDevice>(
			context.DevicesById[_Second.DeviceId],
			context.DeviceIndexesById[_Second.DeviceId],
			_Second,
			_SecondScale,
			_SecondOffset,
			_SecondModifier?.CreateModifierRuntimeContext(context),
			_FirstModifier?.CreateModifierRuntimeContext(context),
			_Mode);

	private sealed class RuntimeModifier<TInputDevice>(
		TInputDevice secondDevice,
		int secondDeviceIndex,
		AxisBinding secondSource,
		double secondScale,
		double secondOffset,
		IRuntimeAxisModifier? secondModifier,
		IRuntimeAxisModifier? firstModifier,
		MergeMode mode) : IRuntimeAxisModifier
		where TInputDevice : JoystickDevice
	{
		// No state of its own, but the child modifiers may be stateful — so
		// the mode is forwarded to every child evaluation.
		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
		{
			var first = firstModifier is { } fm ? fm.Apply(input, states, applyMode) : input;

			double second;
			if (states[secondDeviceIndex] is { } state)
			{
				var raw = secondDevice.ReadNormalizedAxisValue(state, secondSource);
				second = raw * secondScale + secondOffset;
				if (secondModifier is { } sm)
				{
					second = sm.Apply(second, states, applyMode);
				}
			}
			else
			{
				second = secondOffset;
				if (secondModifier is { } sm)
				{
					second = sm.Apply(second, states, applyMode);
				}
			}

			return mode switch
			{
				MergeMode.Sum => first + second,
				MergeMode.Average => (first + second) * 0.5,
				MergeMode.Min => Math.Min(first, second),
				MergeMode.Max => Math.Max(first, second),
				MergeMode.Multiply => first * second,
				_ => first + second,
			};
		}
	}
}