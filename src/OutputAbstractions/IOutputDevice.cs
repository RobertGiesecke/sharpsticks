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

	/// <summary>
	/// Platform default keyboard/mouse synthesizer for this output kind, used by
	/// the console build path when the profile doesn't supply one. The output
	/// device type doubles as the platform marker (vJoy ⇒ Windows), so the
	/// Windows output returns a <c>SendInput</c>-backed synthesizer; platforms
	/// without one yet (Linux uinput) inherit the default <c>null</c>.
	/// </summary>
	static virtual IInputSynthesizer? DefaultInputSynthesizer => null;
}