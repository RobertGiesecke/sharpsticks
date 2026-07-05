namespace SharpSticks.OutputAbstractions;

public interface IOutputRuntimeContext<TInputDevice, TOutputDevice> : IRuntimeContext<TInputDevice>, IDisposable
	where TInputDevice : JoystickDevice
	where TOutputDevice : OutputDevice
{
	string Name { get; }
	ImmutableArray<TOutputDevice> OutputDevices { get; }
	void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null);

	/// <summary>
	/// Process exactly one frame: read every device's current state and apply
	/// all button/axis routes. Intended for deterministic tests; production
	/// code should use <see cref="Run"/>, which waits on device WaitHandles.
	/// </summary>
	void ProcessFrame(DebugLogger? debugLogger = null);

	/// <summary>
	/// The <see cref="Run"/> loop's next wait timeout in milliseconds (<see cref="Timeout.Infinite"/>
	/// = wait for a device event). Floored to <c>UpdateInterval</c> while a continuous
	/// integrator is active. Exposed so tests can assert the steady-tick behavior without
	/// driving the real wait loop.
	/// </summary>
	int ComputeWaitTimeoutMs();
}