namespace SharpSticks.Config;

public enum WhenButtonPressedStateful
{
	None,
	WhenPressed,
	WhenNotPressed,
	Both,
}

public sealed record WhenButtonPressedAxisModifier : IAxisModifier
{
	public required ImmutableArray<ButtonBinding> Buttons { get; init; }
	public IAxisModifier? WhenPressed { get; init; }
	public IAxisModifier? WhenNotPressed { get; init; }

	// Controls which branch(es) integrate input deltas so toggling the
	// modifier buttons doesn't make the output jump. The output is held
	// continuous while the chosen branch is active; switching out of a
	// stateful branch drops the latched state.
	public WhenButtonPressedStateful Stateful { get; init; } = WhenButtonPressedStateful.None;

	private sealed class RuntimeModifier : IRuntimeAxisModifier
	{
		private readonly IRuntimeAxisModifier? _WhenPressed;
		private readonly IRuntimeAxisModifier? _WhenNotPressed;
		private readonly ImmutableArray<RuntimeButtonBinding> _Buttons;
		private readonly WhenButtonPressedStateful _Stateful;

		private bool _HasPrevious;
		private double _LastInput;
		private double _LastOutput;

		private readonly record struct RuntimeButtonBinding
		{
			public required ButtonBinding ButtonBinding { get; init; }
			public required int SourceDeviceIndex { get; init; }
		}

		public RuntimeModifier(IRuntimeContext context, WhenButtonPressedAxisModifier source)
		{
			_WhenPressed = source.WhenPressed?.CreateModifierRuntimeContext(context);
			_WhenNotPressed = source.WhenNotPressed?.CreateModifierRuntimeContext(context);
			_Stateful = source.Stateful;
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
			var isPressed = IsAnyButtonPressed(states);
			var active = isPressed && _WhenPressed is not null ? _WhenPressed : _WhenNotPressed;
			var atInput = ApplyActive(active, input, states);

			var statefulNow = _Stateful switch
			{
				WhenButtonPressedStateful.Both => true,
				WhenButtonPressedStateful.WhenPressed => isPressed && _WhenPressed is not null,
				WhenButtonPressedStateful.WhenNotPressed => !isPressed || _WhenPressed is null,
				_ => false,
			};

			double output;
			if (statefulNow && _HasPrevious)
			{
				// Integrate input deltas through the currently-active branch.
				// The seed is the PREVIOUS frame's output — possibly produced
				// by the non-stateful branch — so entering a stateful branch
				// holds the value where it was. Only further input changes
				// move it, evaluated against the now-active curve.
				output = _LastOutput + (atInput - ApplyActive(active, _LastInput, states));
			}
			else
			{
				output = atInput;
			}

			// Track every frame, not just stateful ones, so the next entry
			// into a stateful branch can seed from the actual previous output.
			_LastInput = input;
			_LastOutput = output;
			_HasPrevious = true;
			return output;
		}

		private bool IsAnyButtonPressed(JoystickState?[] states)
		{
			foreach (var buttonBinding in _Buttons)
			{
				if (states[buttonBinding.SourceDeviceIndex] is not { } state)
				{
					continue;
				}

				if (state.IsButtonPressed(buttonBinding.ButtonBinding.ButtonNumber))
				{
					return true;
				}
			}

			return false;
		}

		private static double ApplyActive(IRuntimeAxisModifier? modifier, double input, JoystickState?[] states) =>
			modifier?.Apply(input, states) ?? input;
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