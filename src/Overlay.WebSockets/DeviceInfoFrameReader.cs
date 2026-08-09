namespace SharpSticks.Overlay.WebSockets;

public ref struct DeviceInfoFrameReader
{
	private readonly byte _DeviceCount;
	private byte _DeviceIndex;
	private int _Offset;
	private readonly ReadOnlySpan<byte> _DeviceFrameSpan;

	public DeviceInfoFrameReader(byte deviceCount, ReadOnlySpan<byte> deviceFrameSpan)
	{
		_DeviceCount = deviceCount;
		_DeviceFrameSpan = deviceFrameSpan;
	}

	/// <summary>Bytes not yet consumed — 0 after the last device proves the
	/// frame parsed to exactly its own length; more than 0 after a false
	/// <see cref="MoveNext"/> tells truncation and completion apart.</summary>
	public int RemainingBytes => _DeviceFrameSpan.Length - _Offset;

	public bool MoveNext(out DeviceInfo deviceInfo)
	{
		deviceInfo = default;
		if (_DeviceIndex >= _DeviceCount)
		{
			return false;
		}

		var frame = _DeviceFrameSpan;
		var pos = _Offset;
		if (frame.Length - pos < 4)
		{
			return false; // truncated header — never read out of bounds
		}

		var isOutput = frame[pos++] == 1;
		var axisCount = frame[pos++];
		var buttonCount = frame[pos++];
		int nameLength = frame[pos++];
		if (frame.Length - pos < nameLength + axisCount)
		{
			return false; // truncated name/axes
		}

		var nameSpan = frame.Slice(pos, nameLength);
		pos += nameLength;
		var axes = frame.Slice(pos, axisCount);

		_Offset = pos + axisCount;
		_DeviceIndex += 1;
		deviceInfo = new()
		{
			IsOutput = isOutput,
			NameBytes = nameSpan,
			AxesBytes = axes,
			ButtonCount = buttonCount,
		};

		return true;
	}
}