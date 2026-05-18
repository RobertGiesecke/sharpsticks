namespace SharpSticks.Config;

public sealed record ButtonMapping
{
	public required ButtonBinding SourceBinding { get; init; }
	public uint? VJoyDeviceId { get; init; }
	public required int TargetButton { get; init; }
}