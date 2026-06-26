namespace SharpSticks.InputAbstractions;

internal sealed record MergeAxesModifier : IAxisModifier,
	IMergeableObject<MergeAxesModifier>
{
	private AxisBinding Second { get; init; }
	private readonly double _SecondScale;
	private readonly double _SecondOffset;
	private IAxisModifier? SecondModifier { get; init; }
	private IAxisModifier? FirstModifier { get; init; }
	private readonly MergeMode _Mode;

	public MergeAxesModifier(
		AxisBinding second,
		double secondScale,
		double secondOffset,
		IAxisModifier? secondModifier,
		IAxisModifier? firstModifier,
		MergeMode mode)
	{
		Second = second;
		_SecondScale = secondScale;
		_SecondOffset = secondOffset;
		SecondModifier = secondModifier;
		FirstModifier = firstModifier;
		_Mode = mode;
	}

	public void FillDevices(ICollection<int> deviceIds)
	{
		deviceIds.Add(Second.DeviceId);
		SecondModifier?.FillDevices(deviceIds);
		FirstModifier?.FillDevices(deviceIds);
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new RuntimeModifier<TInputDevice>(
			context.DevicesById[Second.DeviceId],
			context.DeviceIndexesById[Second.DeviceId],
			Second,
			_SecondScale,
			_SecondOffset,
			SecondModifier?.CreateModifierRuntimeContext(context),
			FirstModifier?.CreateModifierRuntimeContext(context),
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

	public MergeAxesModifier Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = FirstModifier?.MergeOrGet(context, ref hasChanged);
		var x2 = SecondModifier?.MergeOrGet(context, ref hasChanged);
		var x3 = Second.MergeOrGet(context, ref hasChanged);

		return !hasChanged
			? this
			: this with
			{
				FirstModifier = x1,
				SecondModifier = x2,
				Second = x3,
			};
	}
}