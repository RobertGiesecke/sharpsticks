namespace SharpSticks.OverlayServer;

public enum OverlayServeFailReason
{
	None = 0,
	NoInputDevices,
	WebRootDirectoryNotFound,
	RuntimeOptionsInferenceFailed
}