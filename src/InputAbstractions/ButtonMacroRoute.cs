namespace SharpSticks.InputAbstractions;

/// <summary>
/// Source-button-driven macro route. Edge-triggers <see cref="OnPress"/> when
/// <see cref="Binding"/> transitions from released to pressed, and
/// <see cref="OnRelease"/> on the inverse edge. Press and release runs share a
/// single FIFO so event order is preserved.
/// </summary>
public sealed record ButtonMacroRoute : IBoundRoute
{
	public required ButtonBinding Binding { get; init; }
	public ImmutableArray<IMacroAction> OnPress { get; init; } = [];
	public ImmutableArray<IMacroAction> OnRelease { get; init; } = [];

	public const MacroReentry DefaultReentry = MacroReentry.QueueUntilDone;
	public MacroReentry Reentry { get; init; } = DefaultReentry;

	InputBinding IBoundRoute.InputBinding => Binding;

	// A macro can write to multiple output devices; the actual targets are
	// discovered by walking actions via IMacroAction.FillOutputs.
	uint IBoundRoute.OutputDeviceId => 0;
}
