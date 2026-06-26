namespace SharpSticks.Config;

public enum WhenButtonPressedStateful
{
	None,
	WhenPressed,
	WhenNotPressed,
	Both,
}

public sealed record WhenButtonPressedAxisModifier :
	IAxisModifier,
	IMergeableObject<WhenButtonPressedAxisModifier>
{
	public required ImmutableArray<ButtonBinding> Buttons { get; init; }
	public IAxisModifier? WhenPressed { get; init; }
	public IAxisModifier? WhenNotPressed { get; init; }

	// Controls which branch(es) integrate input deltas so toggling the
	// modifier buttons doesn't make the output jump. The output is held
	// continuous while the chosen branch is active; switching out of a
	// stateful branch drops the latched state.
	public WhenButtonPressedStateful Stateful { get; init; } = WhenButtonPressedStateful.None;

	private sealed record RuntimeModifier<TInputDevice> :
		StatefulRuntimeInputModifier<double, RuntimeModifier<TInputDevice>.BranchState>,
		IRuntimeAxisModifier
		where TInputDevice : JoystickDevice
	{
		internal struct BranchState
		{
			public bool HasPrevious;
			public double LastInput;
			public double LastOutput;
			public double LastBranchOutput;
			public IRuntimeAxisModifier? LastActive;
		}

		private readonly IRuntimeAxisModifier? _WhenPressed;
		private readonly IRuntimeAxisModifier? _WhenNotPressed;
		private readonly ImmutableArray<RuntimeButtonBinding> _Buttons;
		private readonly WhenButtonPressedStateful _Stateful;

		private readonly record struct RuntimeButtonBinding
		{
			public required ButtonBinding ButtonBinding { get; init; }
			public required int SourceDeviceIndex { get; init; }
		}

		public RuntimeModifier(IRuntimeContext<TInputDevice> context, WhenButtonPressedAxisModifier source)
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

		protected override double Apply(double input, JoystickState?[] states, ref BranchState state, ApplyMode mode)
		{
			var isPressed = IsAnyButtonPressed(states);
			var active = isPressed && _WhenPressed is not null ? _WhenPressed : _WhenNotPressed;
			var atInput = ApplyActive(active, input, states, mode);

			var statefulNow = _Stateful switch
			{
				WhenButtonPressedStateful.Both => true,
				WhenButtonPressedStateful.WhenPressed => isPressed && _WhenPressed is not null,
				WhenButtonPressedStateful.WhenNotPressed => !isPressed || _WhenPressed is null,
				_ => false,
			};

			double output;
			if (statefulNow && state.HasPrevious)
			{
				// Integrate branch-output deltas onto the previous output.
				// The subtrahend is where the active branch "was" last frame:
				// - same branch: its actual previous output, so the branch's
				//   own evolution (e.g. a stateful child's fade toward its
				//   normal curve) flows through unchanged;
				// - branch just switched: probe the now-active branch at the
				//   previous input — as a Peek, so a stateful child is not
				//   mutated twice per frame — which holds the output where
				//   it was. Only further input changes move it, evaluated
				//   against the now-active curve.
				var previousBranchOutput = ReferenceEquals(active, state.LastActive)
					? state.LastBranchOutput
					: ApplyActive(active, state.LastInput, states, ApplyMode.Peek);
				output = state.LastOutput + (atInput - previousBranchOutput);
			}
			else
			{
				output = atInput;
			}

			// Track every frame, not just stateful ones, so the next entry
			// into a stateful branch can seed from the actual previous output.
			state.LastActive = active;
			state.LastBranchOutput = atInput;
			state.LastInput = input;
			state.LastOutput = output;
			state.HasPrevious = true;

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

		private static double ApplyActive(
			IRuntimeAxisModifier? modifier,
			double input,
			JoystickState?[] states,
			ApplyMode mode) =>
			modifier?.Apply(input, states, mode) ?? input;
	}

	public IRuntimeAxisModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice =>
		new RuntimeModifier<TInputDevice>(context, this);

	public void FillDevices(ICollection<int> deviceIds)
	{
		foreach (var buttonBinding in Buttons)
		{
			deviceIds.Add(buttonBinding.DeviceId);
		}

		WhenPressed?.FillDevices(deviceIds);
		WhenNotPressed?.FillDevices(deviceIds);
	}

	public WhenButtonPressedAxisModifier Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = WhenNotPressed?.MergeOrGet(context, ref hasChanged);
		var x2 = WhenPressed?.MergeOrGet(context, ref hasChanged);

		if (!hasChanged)
		{
			return this;
		}

		return this with
		{
			WhenNotPressed = x1,
			WhenPressed = x2,
		};
	}
}