namespace SharpSticks.OverlayServer;

public readonly record struct OverlayServeResult
{
	public bool Success => FailReason == OverlayServeFailReason.None;
	public OverlayServeFailReason FailReason { get; init; }
}