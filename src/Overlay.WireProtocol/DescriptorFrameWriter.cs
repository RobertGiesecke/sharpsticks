namespace SharpSticks.Overlay.WireProtocol;

/// <summary>
/// The write-side mirror of <see cref="DescriptorFrameReader"/>: emits the
/// frame header on construction, then one descriptor entry per
/// <c>TryWriteDevice</c>. All writes are bounds-checked; a false return means
/// the buffer is too small (nothing was written).
/// </summary>
public ref struct DescriptorFrameWriter
{
	private readonly Span<byte> _Frame;

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
		BytesWritten = HeaderSize;
	}

	public const int HeaderSize = 3; // [0x01][ver][deviceCount]

	public int BytesWritten { get; private set; }

	public static int MeasureDevice(int nameByteCount, int axisCount) =>
		4 + nameByteCount + axisCount; // kind, axisCount, buttonCount, nameLen

	/// <summary>Writes one entry; <paramref name="axisCodes"/> carries the wire's
	/// one-byte axis identities (the typed overload lives with the axis enum).</summary>
	public bool TryWriteDevice(
		bool isOutput,
		scoped ReadOnlySpan<byte> nameUtf8,
		scoped ReadOnlySpan<byte> axisCodes,
		byte buttonCount)
	{
		if (nameUtf8.Length > byte.MaxValue || axisCodes.Length > byte.MaxValue ||
		    _Frame.Length - BytesWritten < MeasureDevice(nameUtf8.Length, axisCodes.Length))
		{
			return false;
		}

		var pos = BytesWritten;
		_Frame[pos++] = isOutput ? (byte)1 : (byte)0;
		_Frame[pos++] = (byte)axisCodes.Length;
		_Frame[pos++] = buttonCount;
		_Frame[pos++] = (byte)nameUtf8.Length;
		nameUtf8.CopyTo(_Frame[pos..]);
		pos += nameUtf8.Length;
		axisCodes.CopyTo(_Frame[pos..]);
		BytesWritten = pos + axisCodes.Length;
		return true;
	}

	/// <summary>Writes an entry from its read-side view — <c>write(read(x)) == x</c>.</summary>
	public bool TryWriteDevice(scoped DeviceInfo deviceInfo)
	{
		if (deviceInfo.NameBytes.Length > byte.MaxValue || deviceInfo.AxesBytes.Length > byte.MaxValue ||
		    _Frame.Length - BytesWritten < MeasureDevice(deviceInfo.NameBytes.Length, deviceInfo.AxesBytes.Length))
		{
			return false;
		}

		var pos = BytesWritten;
		_Frame[pos++] = deviceInfo.IsOutput ? (byte)1 : (byte)0;
		_Frame[pos++] = (byte)deviceInfo.AxesBytes.Length;
		_Frame[pos++] = deviceInfo.ButtonCount;
		_Frame[pos++] = (byte)deviceInfo.NameBytes.Length;
		deviceInfo.NameBytes.CopyTo(_Frame[pos..]);
		pos += deviceInfo.NameBytes.Length;
		deviceInfo.AxesBytes.CopyTo(_Frame[pos..]);
		BytesWritten = pos + deviceInfo.AxesBytes.Length;
		return true;
	}
}