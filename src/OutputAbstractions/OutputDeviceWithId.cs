namespace SharpSticks.OutputAbstractions;

public sealed class OutputDeviceWithId : IOutputDevice
{
	public required uint DeviceId { get; init; }
}