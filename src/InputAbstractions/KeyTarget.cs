using SharpSticks.InputSynthesis.Keyboard;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// A keyboard key as a <see cref="ButtonTarget"/>: held down while asserted (a key tap
/// when driven by a Pulse zone). <see cref="Key"/> accepts <see cref="NamedKey"/> implicitly.
/// </summary>
public sealed record KeyTarget(Key Key) : ButtonTarget
{
    public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
        new KeySink(RequireSynthesizer(context), Key);
}
