namespace SharpSticks.InputAbstractions;

/// <summary>
/// Per-macro-instance context passed to <see cref="IMacroAction.Step"/>. The
/// engine reuses a single context per running macro across frames and refreshes
/// <see cref="Now"/> before each step.
/// </summary>
public sealed class MacroContext
{
	private readonly IMacroOutputSink _Sink;

	public MacroContext(IMacroOutputSink sink, long frequency)
	{
		_Sink = sink;
		Frequency = frequency;
	}

	public long Now { get; private set; }
	public long Frequency { get; }

	public void Press(OutputButtonStateIndex button) => _Sink.Press(button);
	public void Release(OutputButtonStateIndex button) => _Sink.Release(button);

	/// <summary>
	/// Convert a duration relative to "now" into an absolute tick deadline in
	/// the time source's units.
	/// </summary>
	public long DeadlineFromNow(TimeSpan duration) =>
		Now + (long)(duration.TotalSeconds * Frequency);

	public long DeadlineFromNow(long durationInTicks) =>
		Now + durationInTicks;

	/// <summary>
	/// Engine-only: refresh <see cref="Now"/> before stepping the next action.
	/// Action authors should not need this — the value they observe via
	/// <see cref="Now"/> is always the current frame's tick.
	/// </summary>
	public void Refresh(long now) => Now = now;
}

/// <summary>
/// Sink that bridges <see cref="MacroContext.Press"/> / <see cref="MacroContext.Release"/>
/// back to the engine, which records held buttons per macro session and
/// releases them automatically on macro completion or cancellation.
/// </summary>
public interface IMacroOutputSink
{
	void Press(OutputButtonStateIndex button);
	void Release(OutputButtonStateIndex button);
}
