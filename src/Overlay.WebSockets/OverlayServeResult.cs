namespace SharpSticks.Overlay.WebSockets;

public readonly record struct OverlayServeResult
{
	public bool Success => FailReason == OverlayServeFailReason.None;
	public OverlayServeFailReason FailReason { get; init; }
}