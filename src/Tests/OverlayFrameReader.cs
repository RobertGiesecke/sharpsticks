using System.Buffers.Binary;
using System.Text;

namespace SharpSticks.Tests;

/// <summary>
/// Test-side decoder for the overlay's binary frames — the inverse of
/// <see cref="OverlayProtocol"/>. Tests assert against this deserialized form instead
/// of raw byte offsets; every decode doubles as a grammar check because a
/// frame that doesn't parse to exactly its own length throws.
/// Decoded <c>Buttons[i]</c> is button number <c>i + 1</c>.
/// </summary>
internal static class OverlayFrameReader
{
	public sealed record Descriptor(byte Version, IReadOnlyList<Device> Devices);

	public sealed record Device(bool IsOutput, string Name, IReadOnlyList<Axis> Axes, int ButtonCount);

	public sealed record State(byte Version, IReadOnlyList<DeviceState> Devices);

	public sealed record DeviceState(IReadOnlyList<double> Axes, IReadOnlyList<bool> Buttons);

	public static Descriptor ReadDescriptor(ReadOnlySpan<byte> frame)
	{
		if (frame[0] != 0x01)
		{
			throw new InvalidDataException($"Not a descriptor frame: 0x{frame[0]:X2}.");
		}

		var version = frame[1];
		int deviceCount = frame[2];
		var pos = 3;
		var devices = new List<Device>(deviceCount);
		for (var i = 0; i < deviceCount; i++)
		{
			var isOutput = frame[pos++] == 1;
			int axisCount = frame[pos++];
			int buttonCount = frame[pos++];
			int nameLength = frame[pos++];
			var name = Encoding.UTF8.GetString(frame.Slice(pos, nameLength));
			pos += nameLength;
			var axes = new Axis[axisCount];
			for (var a = 0; a < axisCount; a++)
			{
				axes[a] = (Axis)frame[pos++];
			}

			devices.Add(new(isOutput, name, axes, buttonCount));
		}

		return pos == frame.Length
			? new(version, devices)
			: throw new InvalidDataException($"Descriptor has {frame.Length - pos} trailing byte(s).");
	}

	public static State ReadState(ReadOnlySpan<byte> frame, Descriptor descriptor)
	{
		if (frame[0] != 0x02)
		{
			throw new InvalidDataException($"Not a state frame: 0x{frame[0]:X2}.");
		}

		var version = frame[1];
		var pos = 2;
		var devices = new List<DeviceState>(descriptor.Devices.Count);
		foreach (var device in descriptor.Devices)
		{
			var axes = new double[device.Axes.Count];
			for (var a = 0; a < axes.Length; a++)
			{
				axes[a] = BinaryPrimitives.ReadInt16LittleEndian(frame[pos..]) / 32767.0;
				pos += 2;
			}

			var buttons = new bool[device.ButtonCount];
			for (var b = 0; b < buttons.Length; b++)
			{
				buttons[b] = (frame[pos + (b >> 3)] & (1 << (b & 7))) != 0;
			}

			pos += (device.ButtonCount + 7) / 8;
			devices.Add(new(axes, buttons));
		}

		return pos == frame.Length
			? new(version, devices)
			: throw new InvalidDataException($"State frame has {frame.Length - pos} trailing byte(s).");
	}
}
