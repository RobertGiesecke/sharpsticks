using System.Collections.Immutable;

namespace SharpSticks.Overlay.WebSockets;

public readonly record struct OverlayServeRuntimeOptions<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public required string? WebRoot { get; init; }
	public required string? WebRootPath { get; init; }
	public required ushort Port { get; init; }
	public required ImmutableArray<TInputDevice> Devices { get; init; }
}