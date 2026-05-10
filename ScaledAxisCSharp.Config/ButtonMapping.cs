namespace ScaledAxisCSharp.Config;

public sealed record ButtonMapping
{
	public required ButtonBinding SourceBinding { get; init; }
	public int? VJoyDeviceId { get; init; }
	public required int TargetButton { get; init; }
}