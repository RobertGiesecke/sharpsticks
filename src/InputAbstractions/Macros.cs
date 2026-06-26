namespace SharpSticks.InputAbstractions;

public static class Macros
{
	public static IMacroAction Press(this OutputButtonBinding button) => new PressAction(button);
	public static IMacroAction Release(this OutputButtonBinding button) => new ReleaseAction(button);
	public static IMacroAction WaitFor(TimeSpan duration) => new WaitAction(duration);

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
}