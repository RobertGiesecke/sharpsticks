namespace SharpSticks.Overlay.WebSockets;

public readonly record struct ServeOptions<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public string? WebRoot { get; init; }
	public string? WebRootPath { get; init; }
	public ushort? Port { get; init; }
	public Func<TInputDevice, bool>? DevicePredicate { get; init; }

	/// <summary>
	/// Also serve the input-side mirrors of virtual output devices (vJoy on Windows,
	/// SharpSticks' uinput devices on Linux). Off by default: the standalone server
	/// runs alongside the routing engine, and holding a virtual output's mirror open
	/// blocks the engine from acquiring/feeding it. The in-process integration
	/// (<c>ServeOverlay</c>) doesn't go through this filter — there the runtime owns
	/// the feeder handle, so serving the mirrors is safe.
	/// </summary>
	public bool IncludeOutputDevices { get; init; }

	public OverlayServeEvents<TInputDevice>? Events { get; init; }
}

public readonly record struct OverlayServeEvents<TInputDevice> 
	where TInputDevice : JoystickDevice
{
	public Action<OverlayServeRuntimeOptions<TInputDevice>>? Starting { get; init; }
	public Action<OverlayServeRuntimeOptions<TInputDevice>>? Started { get; init; }
}