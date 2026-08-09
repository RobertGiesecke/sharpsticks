namespace SharpSticks.Overlay.WireProtocol;

public readonly ref struct StateFrameReader
{
	private readonly ReadOnlySpan<byte> _Frame;

	public StateFrameReader(ReadOnlySpan<byte> frame)
	{
		_Frame = frame;
	}

	public RefOption<byte> GetVersion() => _Frame.Length switch
	{
		> 1 => new() { Value = _Frame[1] },
		_ => new() { Success = false },
	};

	// A state frame carries no device count — the shape comes from the
	// descriptor, one (axisCount, buttonCount) pair per MoveNext.
	public DeviceStateFrameReader GetDeviceStateReader() =>
		new(_Frame.Length >= 2 ? _Frame[2..] : default);
}