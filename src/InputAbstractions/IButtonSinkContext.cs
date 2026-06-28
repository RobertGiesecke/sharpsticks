namespace SharpSticks.InputAbstractions;

/// <summary>
/// What a <see cref="ButtonTarget"/> needs to build its runtime
/// <see cref="IButtonStateSink"/>. Implemented by the runtime and passed to
/// <see cref="ButtonTarget.CreateRuntimeSink"/> once at build time, mirroring the
/// modifier <c>CreateModifierRuntimeContext</c> pattern.
///
/// <para>vJoy buttons need an <c>OutputDevice</c> (which lives in the output layer),
/// so the target delegates their construction to <see cref="CreateOutputButtonSink"/>
/// rather than building them itself; synthesizer-backed sinks (key/mouse/scroll) it
/// builds directly from <see cref="Synthesizer"/>.</para>
/// </summary>
public interface IButtonSinkContext
{
    /// <summary>The OS keyboard/mouse sink, or <c>null</c> when the profile has none.</summary>
    IInputSynthesizer? Synthesizer { get; }

    /// <summary>Builds the sink that drives a vJoy output button (resolves the output device).</summary>
    IButtonStateSink CreateOutputButtonSink(OutputButtonBinding binding);
}
