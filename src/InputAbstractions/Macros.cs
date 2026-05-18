namespace SharpSticks.InputAbstractions;

public static class Macros
{
	public static IMacroAction Press(this OutputButtonBinding button) => new PressAction(button);
	public static IMacroAction Release(this OutputButtonBinding button) => new ReleaseAction(button);
	public static IMacroAction WaitFor(TimeSpan duration) => new WaitAction(duration);

	private sealed class PressAction(OutputButtonBinding button) : IMacroAction
	{
		public MacroStatus Step(MacroContext ctx)
		{
			ctx.Press(button);
			return MacroStatus.Done;
		}

		public void FillOutputs(ICollection<OutputButtonBinding> outputs) => outputs.Add(button);
	}

	private sealed class ReleaseAction(OutputButtonBinding button) : IMacroAction
	{
		public MacroStatus Step(MacroContext ctx)
		{
			ctx.Release(button);
			return MacroStatus.Done;
		}

		public void FillOutputs(ICollection<OutputButtonBinding> outputs) => outputs.Add(button);
	}

	private sealed class WaitAction(TimeSpan duration) : IMacroAction
	{
		public MacroStatus Step(MacroContext ctx) =>
			MacroStatus.WaitUntil(ctx.DeadlineFromNow(duration));

		public void FillOutputs(ICollection<OutputButtonBinding> outputs) { }
	}
}
