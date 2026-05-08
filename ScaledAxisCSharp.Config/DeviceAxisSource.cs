using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Config;

public sealed record DeviceAxisSource
{
	public required string DeviceName { get; init; }
	public required PhysicalAxis Axis { get; init; }
}