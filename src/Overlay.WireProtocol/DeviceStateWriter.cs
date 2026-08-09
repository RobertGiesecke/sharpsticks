using System.Buffers.Binary;

namespace SharpSticks.Overlay.WireProtocol;

/// <summary>Fills one device's slot in a state frame — the write-side mirror
/// of <see cref="DeviceStateInfo"/>.</summary>
public readonly ref struct DeviceStateWriter
{
	private readonly Span<byte> _Span;
	private readonly int _AxesSize;

	public DeviceStateWriter(Span<byte> span, int axesSize)
	{
		_Span = span;
		_AxesSize = axesSize;
	}

	public void WriteRawAxisValue(int index, short value) =>
		BinaryPrimitives.WriteInt16LittleEndian(_Span[(index * 2)..], value);

	/// <summary>Normalized [-1, 1], clamped and quantized to int16.</summary>
	public void WriteAxisValue(int index, double normalized)
	{
		var clamped = Math.Clamp(normalized, -1.0, 1.0);
		WriteRawAxisValue(index, (short)Math.Round(clamped * 32767.0));
	}

	/// <summary>1-based like <see cref="DeviceStateInfo.IsButtonPressed"/>.</summary>
	public void SetButton(int buttonNumber)
	{
		var bit = buttonNumber - 1;
		_Span[_AxesSize + (bit >> 3)] |= (byte)(1 << (bit & 7));
	}
}