using SharpSticks.InputAbstractions.Keyboard;
using SharpSticks.InputAbstractions.Mouse;

namespace SharpSticks.InputAbstractions;

public static class Macros
{
	public static IMacroAction Press(this OutputButtonBinding button) => new PressAction(button);
	public static IMacroAction Release(this OutputButtonBinding button) => new ReleaseAction(button);
	public static IMacroAction WaitFor(TimeSpan duration) => new WaitAction(duration);

	/// <summary>Synthesize a key press. <paramref name="key"/> accepts <see cref="NamedKey"/> implicitly.</summary>
	public static IMacroAction PressKey(Key key) => new KeyAction(key, Down: true);

	/// <summary>Synthesize a key release.</summary>
	public static IMacroAction ReleaseKey(Key key) => new KeyAction(key, Down: false);

	/// <summary>Synthesize a mouse-button press.</summary>
	public static IMacroAction PressMouseButton(OutputMouseButton button) => new MouseButtonAction(button, Down: true);

	/// <summary>Synthesize a mouse-button release.</summary>
	public static IMacroAction ReleaseMouseButton(OutputMouseButton button) => new MouseButtonAction(button, Down: false);

	private static IInputSynthesizer GetSynthesizerOrThrow(IRuntimeContext runtimeContext) =>
		runtimeContext.InputSynthesizer ?? throw new InvalidOperationException(
			"A keyboard/mouse macro action was used, but no IInputSynthesizer was provided to the runtime.");

	private static OutputButtonStateIndex GetOutputStateIndexOrThrow(IRuntimeContext runtimeContext, OutputButtonBinding button)
	{
		return runtimeContext.TryGetOutputStateIndex(button) ?? throw new InvalidOperationException($"Could not find output state for button {button}");
	}

	private sealed class PressAction(OutputButtonBinding button) : IMacroAction, IMergeableObject<PressAction>
	{
		public void FillOutputs(ICollection<OutputButtonBinding> outputs) => outputs.Add(button);

		IRuntimeMacroAction IMacroAction.CreateRuntimeAction(IRuntimeContext runtimeContext) =>
			new RuntimeAction(GetOutputStateIndexOrThrow(runtimeContext, button));


		sealed class RuntimeAction : IRuntimeMacroAction
		{
			private readonly OutputButtonStateIndex _OutputStateIndex;

			public RuntimeAction(OutputButtonStateIndex outputStateIndex)
			{
				_OutputStateIndex = outputStateIndex;
			}
			public MacroStatus Step(MacroContext ctx)
			{
				ctx.Press(_OutputStateIndex);
				return MacroStatus.Done;
			}
		}

		public PressAction Merge(MergeObjectContext context)
		{
			var hasChanges = false;
			var x1 = button.MergeOrGet(context, ref hasChanges);

			return !hasChanges
				? this
				: new(x1);
		}
	}

	private sealed class ReleaseAction(OutputButtonBinding button) : IMacroAction, IMergeableObject<ReleaseAction>
	{
		IRuntimeMacroAction IMacroAction.CreateRuntimeAction(IRuntimeContext runtimeContext) => 
			new RuntimeAction(GetOutputStateIndexOrThrow(runtimeContext, button));

		sealed class RuntimeAction : IRuntimeMacroAction
		{
			private readonly OutputButtonStateIndex _OutputStateIndex;

			public RuntimeAction(OutputButtonStateIndex outputStateIndex)
			{
				_OutputStateIndex = outputStateIndex;
			}

			public MacroStatus Step(MacroContext ctx)
			{
				ctx.Release(_OutputStateIndex);
				return MacroStatus.Done;
			}
		}

		public void FillOutputs(ICollection<OutputButtonBinding> outputs) => outputs.Add(button);
		public ReleaseAction Merge(MergeObjectContext context)
		{
			var hasChanges = false;
			var x1 = button.MergeOrGet(context, ref hasChanges);

			return !hasChanges
				? this
				: new(x1);
		}
	}

	private sealed class WaitAction(TimeSpan duration) : IMacroAction, IMergeableObject<WaitAction>
	{
		IRuntimeMacroAction IMacroAction.CreateRuntimeAction(IRuntimeContext runtimeContext)
		{
			return new RuntimeAction(duration);
		}

		sealed class RuntimeAction : IRuntimeMacroAction
		{
			private readonly TimeSpan _Duration;

			public RuntimeAction(TimeSpan duration)
			{
				_Duration = duration;
			}

			public MacroStatus Step(MacroContext ctx) => 
				MacroStatus.WaitUntil(ctx.DeadlineFromNow(_Duration));
		}
		public void FillOutputs(ICollection<OutputButtonBinding> outputs)
		{
		}

		public WaitAction Merge(MergeObjectContext context) => this;
	}

	// Keyboard/mouse actions drive the OS synthesizer directly — they never touch
	// the vJoy output-button refcount path, so FillOutputs is empty (no output
	// device to open) and they hold no mergeable sub-objects.
	private sealed record KeyAction(Key Key, bool Down) : IMacroAction, IMergeableObject<KeyAction>
	{
		public void FillOutputs(ICollection<OutputButtonBinding> outputs)
		{
		}

		IRuntimeMacroAction IMacroAction.CreateRuntimeAction(IRuntimeContext runtimeContext) =>
			new RuntimeAction(GetSynthesizerOrThrow(runtimeContext), Key, Down);

		public KeyAction Merge(MergeObjectContext context) => this;

		private sealed class RuntimeAction(IInputSynthesizer synthesizer, Key key, bool down) : IRuntimeMacroAction
		{
			public MacroStatus Step(MacroContext ctx)
			{
				if (down)
				{
					synthesizer.KeyDown(key);
				}
				else
				{
					synthesizer.KeyUp(key);
				}

				return MacroStatus.Done;
			}
		}
	}

	private sealed record MouseButtonAction(OutputMouseButton Button, bool Down)
		: IMacroAction, IMergeableObject<MouseButtonAction>
	{
		public void FillOutputs(ICollection<OutputButtonBinding> outputs)
		{
		}

		IRuntimeMacroAction IMacroAction.CreateRuntimeAction(IRuntimeContext runtimeContext) =>
			new RuntimeAction(GetSynthesizerOrThrow(runtimeContext), Button, Down);

		public MouseButtonAction Merge(MergeObjectContext context) => this;

		private sealed class RuntimeAction(IInputSynthesizer synthesizer, OutputMouseButton button, bool down)
			: IRuntimeMacroAction
		{
			public MacroStatus Step(MacroContext ctx)
			{
				if (down)
				{
					synthesizer.MouseButtonDown(button);
				}
				else
				{
					synthesizer.MouseButtonUp(button);
				}

				return MacroStatus.Done;
			}
		}
	}
}