namespace ScaledAxisCSharp.OutputAbstractions;

public interface IOutputRuntimeContext : IRuntimeContext, IDisposable
{
	string Name { get; }
	ImmutableArray<OutputDevice> OutputDevices { get; }
	void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null);

	/// <summary>
	/// Process exactly one frame: read every device's current state and apply
	/// all button/axis routes. Intended for deterministic tests; production
	/// code should use <see cref="Run"/>, which waits on device WaitHandles.
	/// </summary>
	void ProcessFrame(DebugLogger? debugLogger = null);
}