namespace SharpSticks.OutputAbstractions;

public interface IOutputDevice
{
	uint DeviceId { get; }

	/// <summary>
	/// Commit this frame's writes to the backend in one batch — e.g. a vJoy
	/// position update, or a Windows <c>SendInput</c> / Linux uinput <c>SYN</c>
	/// for synthesized keyboard/mouse events. Backends that apply each
	/// <c>Set…</c> immediately leave this a no-op (the default). Called once per
	/// frame by the runtime after all routes have been applied.
	/// </summary>
	void Flush() { }
}

public interface IOutputDeviceWithFactory<TSelf> : IOutputDevice
	where TSelf : OutputDevice
{
	static abstract IOutputDeviceFactory<TSelf> Factory { get; }
}