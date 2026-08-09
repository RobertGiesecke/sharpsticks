namespace SharpSticks.Overlay.WebSockets;

/// <summary>First byte of every overlay frame — the wire grammar's tag.</summary>
public enum FrameType : byte
{
	FrameDescriptor = 0x01,
	FrameState = 0x02,
}
