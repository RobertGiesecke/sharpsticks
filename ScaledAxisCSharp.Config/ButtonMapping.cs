namespace ScaledAxisCSharp.Config;

public sealed record ButtonMapping
{
	public required ButtonBinding SourceBinding { get; init; }
	public required int TargetButton { get; init; }
}