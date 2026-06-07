namespace SharpSticks.InputAbstractions;

public interface IRuntimeContext<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public FrozenDictionary<int, TInputDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<TInputDevice> Devices { get; }

	/// <summary>
	/// The runtime's clock — modifiers that integrate over wall-clock time
	/// (rather than per frame) read it to compute elapsed seconds between
	/// frames. Tests inject a fake to advance virtual time deterministically.
	/// </summary>
	public ITimeSource TimeSource { get; }
}