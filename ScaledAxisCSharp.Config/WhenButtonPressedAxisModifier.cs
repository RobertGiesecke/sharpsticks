using System.Collections.Immutable;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Config;

public sealed record WhenButtonPressedAxisModifier : IAxisModifier
{
	public required ImmutableArray<ButtonBinding> Buttons { get; init; }
	public IAxisModifier? WhenPressed { get; init; }
	public IAxisModifier? WhenNotPressed { get; init; }

	public double Apply(double input,
		IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices)
	{
		foreach (var buttonBinding in Buttons)
		{
			if (!states.TryGetValue(buttonBinding.DeviceId, out var state))
			{
				continue;
			}

			if (!state.IsButtonPressed(buttonBinding.ButtonNumber))
			{
				continue;
			}

			if (WhenPressed is null)
			{
				continue;
			}

			return WhenPressed.Apply(input, states, devices);
		}

		return WhenNotPressed?.Apply(input, states, devices) ?? input;
	}
}