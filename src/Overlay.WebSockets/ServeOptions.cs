namespace SharpSticks.Overlay.WebSockets;

public readonly record struct ServeOptions<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public string? WebRoot { get; init; }
	public ushort? Port { get; init; }
	public Func<TInputDevice, bool>? DevicePredicate { get; init; }
	
	public Action<OverlayServeRuntimeOptions<TInputDevice>>? Started { get; init; }
}