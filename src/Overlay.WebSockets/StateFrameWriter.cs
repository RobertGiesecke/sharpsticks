namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// The write-side mirror of <see cref="StateFrameReader"/> /
/// <see cref="DeviceStateFrameReader"/>: emits the frame header on
/// construction, then per device a zeroed slot via <c>TryBeginDevice</c> —
/// axes and buttons are filled through the returned
/// <see cref="DeviceStateWriter"/>; a slot left untouched serializes as
/// centered axes and released buttons.
/// </summary>
public ref struct StateFrameWriter
{
	private readonly Span<byte> _Frame;
	private int _Offset;

	public StateFrameWriter(Span<byte> frame, byte version)
	{
		if (frame.Length < HeaderSize)
		{
			throw new ArgumentException(
				$"A state frame needs at least {HeaderSize} bytes.", nameof(frame));
		}

		frame[0] = (byte)FrameType.FrameState;
		frame[1] = version;
		_Frame = frame;
		_Offset = HeaderSize;
	}

	public const int HeaderSize = 2; // [0x02][ver]

	public int BytesWritten => _Offset;

	public static int MeasureDevice(int axisCount, int buttonCount) =>
		axisCount * 2 + (buttonCount + 7) / 8;

	public bool TryBeginDevice(byte axisCount, byte buttonCount, out DeviceStateWriter deviceWriter)
	{
		var axesSize = axisCount * 2;
		var size = axesSize + (buttonCount + 7) / 8;
		if (_Frame.Length - _Offset < size)
		{
			deviceWriter = default;
			return false;
		}

		var span = _Frame.Slice(_Offset, size);
		span.Clear();
		deviceWriter = new(span, axesSize);
		_Offset += size;
		return true;
	}

	public bool TryBeginDevice(in DeviceInfo deviceInfo, out DeviceStateWriter deviceWriter) =>
		TryBeginDevice((byte)deviceInfo.AxisCount, deviceInfo.ButtonCount, out deviceWriter);
}