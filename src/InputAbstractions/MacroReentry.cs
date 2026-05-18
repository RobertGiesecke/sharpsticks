namespace SharpSticks.InputAbstractions;

public enum MacroReentry
{
	/// <summary>Default. Trigger events while a macro is already running are
	/// enqueued and replayed in order.</summary>
	QueueUntilDone,

	/// <summary>Trigger events while a macro is already running are dropped.</summary>
	DropIfBusy,

	/// <summary>A new trigger cancels the running macro (releasing any held
	/// buttons) and starts the new one immediately.</summary>
	CancelAndRestart,
}
