namespace SharpSticks.Overlay.WireProtocol;

public readonly ref struct DeviceInfoEnumerable
{
	private readonly DescriptorFrameReader _FrameReader;

	public DeviceInfoEnumerable(DescriptorFrameReader frameReader)
	{
		_FrameReader = frameReader;
	}

	// A default DeviceInfoFrameReader enumerates nothing, so parse failure
	// (short frame) and zero devices both fall out as an empty sequence.
	public DeviceInfoEnumerator GetEnumerator() =>
		new(_FrameReader.GetDeviceFrameReader() is { Success: true } option ? option.Value : default);
}