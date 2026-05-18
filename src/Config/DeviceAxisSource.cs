namespace SharpSticks.Config;

public sealed record DeviceAxisSource
{
	public required string DeviceName { get; init; }
	public required Axis Axis { get; init; }
}