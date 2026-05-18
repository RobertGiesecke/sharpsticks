namespace SharpSticks.InputAbstractions;

public interface IRuntimeContext
{
	public FrozenDictionary<int, JoystickDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<JoystickDevice> Devices { get; }
}