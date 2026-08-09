namespace SharpSticks.Overlay.WebSockets;

public ref struct DeviceInfoEnumerator
{
	private DeviceInfo _DeviceInfo;
	private bool _HasCurrent;
	private DeviceInfoFrameReader _DeviceInfoFrameReader;

	public DeviceInfoEnumerator(DeviceInfoFrameReader deviceInfoFrameReader)
	{
		_DeviceInfoFrameReader = deviceInfoFrameReader;
	}

	public DeviceInfo Current =>
		_HasCurrent ? _DeviceInfo : throw new InvalidOperationException("No current device");

	public bool MoveNext()
	{
		return _HasCurrent = _DeviceInfoFrameReader.MoveNext(out _DeviceInfo);
	}
}