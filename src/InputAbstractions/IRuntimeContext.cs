namespace SharpSticks.InputAbstractions;

public interface IRuntimeContext
{
	public FrozenDictionary<int, int> DeviceIndexesById { get; }

	public OutputButtonStateIndex? TryGetOutputStateIndex(OutputButtonBinding binding);

	/// <summary>
	/// The runtime's clock — modifiers that integrate over wall-clock time
	/// (rather than per frame) read it to compute elapsed seconds between
	/// frames. Tests inject a fake to advance virtual time deterministically.
	/// </summary>
	public ITimeSource TimeSource { get; }

	/// <summary>
	/// OS keyboard/mouse event sink that key/mouse macro actions drive, or
	/// <c>null</c> when the profile doesn't synthesize input. Resolved once at
	/// build time by the action; building a key/mouse macro against a runtime
	/// with no synthesizer is a configuration error.
	/// </summary>
	public IInputSynthesizer? InputSynthesizer { get; }
}

public interface IRuntimeContext<TInputDevice> : IRuntimeContext
	where TInputDevice : JoystickDevice
{
	public FrozenDictionary<int, TInputDevice> DevicesById { get; }
	public ImmutableArray<TInputDevice> Devices { get; }
}