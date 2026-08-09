namespace SharpSticks.Overlay.WireProtocol;

public ref struct DeviceStateFrameReader
{
	private int _Offset;
	private readonly ReadOnlySpan<byte> _StateSpan;

	public DeviceStateFrameReader(ReadOnlySpan<byte> stateSpan)
	{
		_StateSpan = stateSpan;
	}

	/// <summary>Bytes not yet consumed — 0 after the last device proves the
	/// frame parsed to exactly its own length.</summary>
	public int RemainingBytes => _StateSpan.Length - _Offset;

	public bool MoveNext(byte axisCount, byte buttonCount, out DeviceStateInfo deviceState)
	{
		var axesSize = axisCount * 2;
		var size = axesSize + (buttonCount + 7) / 8;
		if (RemainingBytes < size)
		{
			// Truncated (or exhausted) frame — never read out of bounds.
			deviceState = default;
			return false;
		}

		deviceState = new()
		{
			AxesBytes = _StateSpan.Slice(_Offset, axesSize),
			ButtonBytes = _StateSpan.Slice(_Offset + axesSize, size - axesSize),
			ButtonCount = buttonCount,
		};
		_Offset += size;
		return true;
	}

	public bool MoveNext(in DeviceInfo deviceInfo, out DeviceStateInfo deviceState) =>
		MoveNext((byte)deviceInfo.AxisCount, deviceInfo.ButtonCount, out deviceState);
}