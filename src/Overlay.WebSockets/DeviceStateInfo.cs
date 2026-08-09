using System.Buffers.Binary;

namespace SharpSticks.Overlay.WebSockets;

/// <summary>One device's state, viewed in place over the frame bytes.</summary>
public readonly ref struct DeviceStateInfo
{
	public required ReadOnlySpan<byte> AxesBytes { get; init; }
	public required ReadOnlySpan<byte> ButtonBytes { get; init; }
	public required byte ButtonCount { get; init; }

	public int AxisCount => AxesBytes.Length / 2;

	public short GetRawAxisValue(int index) =>
		BinaryPrimitives.ReadInt16LittleEndian(AxesBytes[(index * 2)..]);

	/// <summary>Normalized [-1, 1] — the client-side division by 32767.</summary>
	public double GetAxisValue(int index) => GetRawAxisValue(index) / 32767.0;

	/// <summary>1-based like <see cref="JoystickState.IsButtonPressed"/>;
	/// out-of-range numbers and padding bits read as released.</summary>
	public bool IsButtonPressed(int buttonNumber)
	{
		var bit = buttonNumber - 1;
		return (uint)bit < ButtonCount &&
		       (ButtonBytes[bit >> 3] & (1 << (bit & 7))) != 0;
	}
}