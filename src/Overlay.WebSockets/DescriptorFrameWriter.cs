namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// The write-side mirror of <see cref="DescriptorFrameReader"/>: emits the
/// frame header on construction, then one descriptor entry per
/// <c>TryWriteDevice</c>. All writes are bounds-checked; a false return means
/// the buffer is too small (nothing was written).
/// </summary>
public ref struct DescriptorFrameWriter
{
	private readonly Span<byte> _Frame;
	private int _Offset;

	public DescriptorFrameWriter(Span<byte> frame, byte version, byte deviceCount)
	{
		if (frame.Length < HeaderSize)
		{
			throw new ArgumentException(
				$"A descriptor frame needs at least {HeaderSize} bytes.", nameof(frame));
		}

		frame[0] = (byte)FrameType.FrameDescriptor;
		frame[1] = version;
		frame[2] = deviceCount;
		_Frame = frame;
		_Offset = HeaderSize;
	}

	public const int HeaderSize = 3; // [0x01][ver][deviceCount]

	public int BytesWritten => _Offset;

	public static int MeasureDevice(int nameByteCount, int axisCount) =>
		4 + nameByteCount + axisCount; // kind, axisCount, buttonCount, nameLen

	public bool TryWriteDevice(
		bool isOutput,
		ReadOnlySpan<byte> nameUtf8,
		ReadOnlySpan<Axis> axes,
		byte buttonCount)
	{
		if (nameUtf8.Length > byte.MaxValue || axes.Length > byte.MaxValue ||
		    _Frame.Length - _Offset < MeasureDevice(nameUtf8.Length, axes.Length))
		{
			return false;
		}

		var pos = _Offset;
		_Frame[pos++] = isOutput ? (byte)1 : (byte)0;
		_Frame[pos++] = (byte)axes.Length;
		_Frame[pos++] = buttonCount;
		_Frame[pos++] = (byte)nameUtf8.Length;
		nameUtf8.CopyTo(_Frame[pos..]);
		pos += nameUtf8.Length;
		foreach (var axis in axes)
		{
			_Frame[pos++] = (byte)axis;
		}

		_Offset = pos;
		return true;
	}

	/// <summary>Writes an entry from its read-side view — <c>write(read(x)) == x</c>.</summary>
	public bool TryWriteDevice(in DeviceInfo deviceInfo)
	{
		if (deviceInfo.NameBytes.Length > byte.MaxValue || deviceInfo.AxesBytes.Length > byte.MaxValue ||
		    _Frame.Length - _Offset < MeasureDevice(deviceInfo.NameBytes.Length, deviceInfo.AxesBytes.Length))
		{
			return false;
		}

		var pos = _Offset;
		_Frame[pos++] = deviceInfo.IsOutput ? (byte)1 : (byte)0;
		_Frame[pos++] = (byte)deviceInfo.AxesBytes.Length;
		_Frame[pos++] = deviceInfo.ButtonCount;
		_Frame[pos++] = (byte)deviceInfo.NameBytes.Length;
		deviceInfo.NameBytes.CopyTo(_Frame[pos..]);
		pos += deviceInfo.NameBytes.Length;
		deviceInfo.AxesBytes.CopyTo(_Frame[pos..]);
		_Offset = pos + deviceInfo.AxesBytes.Length;
		return true;
	}
}