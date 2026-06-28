using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>A synthesized mouse button as a <see cref="ButtonTarget"/>.</summary>
public sealed record MouseButtonTarget : ButtonTarget
{
	public required OutputMouseButton Button { get; init; }

	public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
		new MouseButtonSink(RequireSynthesizer(context), Button);
}