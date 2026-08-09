namespace SharpSticks.Overlay.WireProtocol;

public readonly ref struct DescriptorFrameReader
{
	private readonly ReadOnlySpan<byte> _Frame;

	public DescriptorFrameReader(ReadOnlySpan<byte> frame)
	{
		_Frame = frame;
	}

	public RefOption<byte> GetVersion() => _Frame.Length switch
	{
		> 1 => new() { Value = _Frame[1] },
		_ => new() { Success = false },
	};

	public RefOption<byte> GetDeviceCount() => _Frame.Length switch
	{
		> 2 => new() { Value = _Frame[2] },
		_ => new() { Success = false },
	};

	public DeviceInfoEnumerable EnumerateDevices() => new(this);

	public RefOption<DeviceInfoFrameReader> GetDeviceFrameReader() => GetDeviceCount() switch
	{
		{ Success: false } => new() { Success = false, },
		{ Value: var d } => RefOption.For(new DeviceInfoFrameReader(d, _Frame[3..])),
	};
}