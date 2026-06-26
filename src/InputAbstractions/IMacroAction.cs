namespace SharpSticks.InputAbstractions;

public interface IRuntimeMacroAction
{
	MacroStatus Step(MacroContext ctx);
}

public interface IMacroAction
{
	/// <summary>
	/// Report every output button this action might write to. The runtime
	/// builder unions these across all macro routes so the corresponding
	/// output devices get opened. Composite actions (If, Sequence) must
	/// recurse into their children.
	/// </summary>
	void FillOutputs(ICollection<OutputButtonBinding> outputs);
	IRuntimeMacroAction CreateRuntimeAction(IRuntimeContext runtimeContext);
}
