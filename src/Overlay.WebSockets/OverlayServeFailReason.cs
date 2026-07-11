namespace SharpSticks.Overlay.WebSockets;

public enum OverlayServeFailReason
{
	None = 0,
	NoInputDevices,
	WebRootDirectoryNotFound,
	RuntimeOptionsInferenceFailed
}