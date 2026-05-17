namespace SharpSticks.InputAbstractions;

public enum MacroStatusKind
{
	Done,
	RunAgainNextFrame,
	WaitUntil,
}

/// <summary>
/// Return value of <see cref="IMacroAction.Step"/>. A struct so action stepping
/// is allocation-free. <see cref="DeadlineTicks"/> is only meaningful when
/// <see cref="Kind"/> is <see cref="MacroStatusKind.WaitUntil"/>.
/// </summary>
public readonly record struct MacroStatus
{
	public MacroStatusKind Kind { get; }
	public long DeadlineTicks { get; }

	private MacroStatus(MacroStatusKind kind, long deadlineTicks)
	{
		Kind = kind;
		DeadlineTicks = deadlineTicks;
	}

	public static MacroStatus Done { get; } = new(MacroStatusKind.Done, 0);
	public static MacroStatus RunAgainNextFrame { get; } = new(MacroStatusKind.RunAgainNextFrame, 0);
	public static MacroStatus WaitUntil(long deadlineTicks) => new(MacroStatusKind.WaitUntil, deadlineTicks);
}
