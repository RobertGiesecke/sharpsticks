namespace SharpSticks.InputAbstractions;

public static class Macros
{
	public static IMacroAction Press(this OutputButtonBinding button) => new PressAction(button);
	public static IMacroAction Release(this OutputButtonBinding button) => new ReleaseAction(button);
	public static IMacroAction WaitFor(TimeSpan duration) => new WaitAction(duration);

	private sealed class PressAction(OutputButtonBinding button) : IMacroAction, IMergeableObject<PressAction>
	{
		public MacroStatus Step(MacroContext ctx)
		{
			ctx.Press(button);
			return MacroStatus.Done;
		}

		public void FillOutputs(ICollection<OutputButtonBinding> outputs) => outputs.Add(button);

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
		public MacroStatus Step(MacroContext ctx)
		{
			ctx.Release(button);
			return MacroStatus.Done;
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
		public MacroStatus Step(MacroContext ctx) =>
			MacroStatus.WaitUntil(ctx.DeadlineFromNow(duration));

		public void FillOutputs(ICollection<OutputButtonBinding> outputs)
		{
		}

		public WaitAction Merge(MergeObjectContext context) => this;
	}
}