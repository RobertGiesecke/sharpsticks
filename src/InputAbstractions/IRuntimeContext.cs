namespace SharpSticks.InputAbstractions;

public interface IRuntimeContext<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public FrozenDictionary<int, TInputDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<TInputDevice> Devices { get; }
}