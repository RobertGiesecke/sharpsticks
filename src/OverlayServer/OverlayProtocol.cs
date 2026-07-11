using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using SharpSticks.InputAbstractions;

namespace SharpSticks.OverlayServer;

/// <summary>
/// Serializes the current device state into the compact little-endian binary frames the
/// joyviz overlay consumes over WebSocket. See <c>docs</c> in the plan for the layout:
///
/// <code>
/// Descriptor (0x01): [0x01][ver][deviceCount]  per device:
///     [kind(0=input,1=output)][axisCount][buttonCount][nameLen][name UTF-8][axisCount x Axis-enum byte]
/// State (0x02):      [0x02][ver]  per device, in descriptor order:
///     axes:    int16 x axisCount   (round(v*32767), client divides by 32767)
///     buttons: ceil(buttonCount/8) bytes, bit i = button (i+1), LSB first
/// </code>
///
/// The descriptor is built once (the device set is fixed for the process lifetime) and the
/// state buffer is allocated once and reused every frame — no per-frame allocation.
/// </summary>
internal static class OverlayProtocol
{
	public static OverlayProtocol<TInputDevice> Create<TInputDevice>(
		ImmutableArray<TInputDevice> devices,
		byte version)
		where TInputDevice : JoystickDevice => new(devices, version);
}
internal sealed class OverlayProtocol<TInputDevice>
	where TInputDevice : JoystickDevice
{
	public const byte FrameDescriptor = 0x01;
	public const byte FrameState = 0x02;

	private readonly ImmutableArray<TInputDevice> _Devices;
	private readonly AxisBinding[][] _AxisBindings;
	private readonly int[] _ButtonCounts;
	private readonly byte _Version;
	private readonly byte[] _StateBuffer;

	public byte[] Descriptor { get; }

	public OverlayProtocol(ImmutableArray<TInputDevice> devices, byte version)
	{
		_Devices = devices;
		_Version = version;
		_AxisBindings = new AxisBinding[devices.Length][];
		_ButtonCounts = new int[devices.Length];

		var stateSize = 2; // [0x02][ver]
		for (var i = 0; i < devices.Length; i++)
		{
			var device = devices[i];
			var axes = device.PhysicalAxes;
			var bindings = new AxisBinding[axes.Length];
			for (var a = 0; a < axes.Length; a++)
			{
				bindings[a] = new(device.DeviceId, axes[a]);
			}

			_AxisBindings[i] = bindings;
			_ButtonCounts[i] = (int)device.Capabilities.NumButtons;
			stateSize += axes.Length * 2 + ByteCount(_ButtonCounts[i]);
		}

		_StateBuffer = new byte[stateSize];
		Descriptor = BuildDescriptor();
	}

	private static int ByteCount(int buttons) => (buttons + 7) / 8;

	private static bool IsOutput(string name) =>
		name.StartsWith("vJoy", StringComparison.OrdinalIgnoreCase);

	private byte[] BuildDescriptor()
	{
		// Size the descriptor exactly, then write it.
		var size = 3; // [0x01][ver][deviceCount]
		var nameBytes = new byte[_Devices.Length][];
		for (var i = 0; i < _Devices.Length; i++)
		{
			var name = _Devices[i].Name ?? "";
			var bytes = Encoding.UTF8.GetBytes(name);
			if (bytes.Length > 255)
			{
				bytes = bytes[..255];
			}

			nameBytes[i] = bytes;
			// kind, axisCount, buttonCount, nameLen, name, axisCount x axis-enum byte
			size += 4 + bytes.Length + _Devices[i].PhysicalAxes.Length;
		}

		var buffer = new byte[size];
		buffer[0] = FrameDescriptor;
		buffer[1] = _Version;
		buffer[2] = (byte)_Devices.Length;
		var pos = 3;
		for (var i = 0; i < _Devices.Length; i++)
		{
			var device = _Devices[i];
			var axes = device.PhysicalAxes;
			buffer[pos++] = (byte)(IsOutput(device.Name ?? "") ? 1 : 0);
			buffer[pos++] = (byte)axes.Length;
			buffer[pos++] = (byte)Math.Min(_ButtonCounts[i], 255);
			buffer[pos++] = (byte)nameBytes[i].Length;
			Array.Copy(nameBytes[i], 0, buffer, pos, nameBytes[i].Length);
			pos += nameBytes[i].Length;
			for (var a = 0; a < axes.Length; a++)
			{
				buffer[pos++] = (byte)axes[a];
			}
		}

		return buffer;
	}

	/// <summary>
	/// Fills the reused state buffer from the latest per-device states and returns the slice
	/// to broadcast. <paramref name="states"/> is indexed like the device array; a null entry
	/// (device unreadable this frame) serializes as centered axes and released buttons.
	/// </summary>
	public ReadOnlySpan<byte> WriteState(JoystickState?[] states)
	{
		var buffer = _StateBuffer;
		Array.Clear(buffer); // zero button bits + any padding up front
		buffer[0] = FrameState;
		buffer[1] = _Version;
		var pos = 2;

		for (var i = 0; i < _Devices.Length; i++)
		{
			var device = _Devices[i];
			var bindings = _AxisBindings[i];
			var state = states[i];

			for (var a = 0; a < bindings.Length; a++)
			{
				var normalized = state is { } s ? device.ReadNormalizedAxisValue(s, bindings[a]) : 0.0;
				BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(pos), ToInt16(normalized));
				pos += 2;
			}

			var buttonCount = _ButtonCounts[i];
			var buttonBytes = ByteCount(buttonCount);
			if (state is { } bs)
			{
				for (var b = 0; b < buttonCount; b++)
				{
					if (bs.IsButtonPressed(b + 1))
					{
						buffer[pos + (b >> 3)] |= (byte)(1 << (b & 7));
					}
				}
			}

			pos += buttonBytes;
		}

		return buffer.AsSpan(0, pos);
	}

	private static short ToInt16(double normalized)
	{
		var clamped = Math.Clamp(normalized, -1.0, 1.0);
		return (short)Math.Round(clamped * 32767.0);
	}
}