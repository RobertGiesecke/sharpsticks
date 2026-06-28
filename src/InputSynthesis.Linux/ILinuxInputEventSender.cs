namespace SharpSticks.InputSynthesis.Linux;

public interface ILinuxInputEventSender
{
	/// <summary>
	/// Create/open the underlying device if it hasn't been already. Idempotent.
	/// </summary>
	void Initialize();

	void Write(LinuxInputEvent ev);
}