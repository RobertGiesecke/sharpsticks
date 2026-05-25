namespace SharpSticks.OutputAbstractions;

public interface IOutputDevice
{
	uint DeviceId { get; }
}

public interface IOutputDeviceWithFactory<TSelf> : IOutputDevice
	where TSelf : OutputDevice
{
	static abstract IOutputDeviceFactory<TSelf> Factory { get; }
}