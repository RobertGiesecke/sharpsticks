namespace ScaledAxisCSharp.Config;

public sealed record WhenButtonPressedAxisModifier : IAxisModifier
{
	public required ImmutableArray<ButtonBinding> Buttons { get; init; }
	public IAxisModifier? WhenPressed { get; init; }
	public IAxisModifier? WhenNotPressed { get; init; }

	private sealed record RuntimeModifier : IRuntimeAxisModifier
	{
		private readonly IRuntimeAxisModifier? _WhenPressed;
		private readonly IRuntimeAxisModifier? _WhenNotPressed;
		private readonly ImmutableArray<RuntimeButtonBinding> _Buttons;

		private readonly record struct RuntimeButtonBinding
		{
			public required ButtonBinding ButtonBinding { get; init; }
			public required int SourceDeviceIndex { get; init; }
		}

		public RuntimeModifier(IRuntimeContext context, WhenButtonPressedAxisModifier source)
		{
			_WhenPressed = source.WhenPressed?.CreateModifierRuntimeContext(context);
			_WhenNotPressed = source.WhenNotPressed?.CreateModifierRuntimeContext(context);
			_Buttons =
			[
				..source.Buttons.Select(b => new RuntimeButtonBinding
				{
					ButtonBinding = b,
					SourceDeviceIndex = context.DeviceIndexesById[b.DeviceId],
				}),
			];
		}

		public double Apply(double input, JoystickState?[] states)
		{
			foreach (var buttonBinding in _Buttons)
			{
				if (states[buttonBinding.SourceDeviceIndex] is not { } state)
				{
					continue;
				}

				if (!state.IsButtonPressed(buttonBinding.ButtonBinding.ButtonNumber))
				{
					continue;
				}

				if (_WhenPressed is null)
				{
					continue;
				}

				return _WhenPressed.Apply(input, states);
			}

			return _WhenNotPressed?.Apply(input, states) ?? input;
		}
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext(IRuntimeContext context) =>
		new RuntimeModifier(context, this);

	public void FillDevices(ICollection<int> deviceIds)
	{
		foreach (var buttonBinding in Buttons)
		{
			deviceIds.Add(buttonBinding.DeviceId);
		}

		WhenPressed?.FillDevices(deviceIds);
		WhenNotPressed?.FillDevices(deviceIds);
	}
}