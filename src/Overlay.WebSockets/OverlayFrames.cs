namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// Convenience layer over the streaming frame primitives: an allocating,
/// record-shaped decode for tools and tests. Built entirely on
/// <see cref="FrameReader"/> and friends, so the wire grammar has exactly one
/// implementation; unlike the primitives, these throw
/// <see cref="InvalidDataException"/> on any malformed frame.
/// </summary>
public static class OverlayFrames
{
	public sealed record Descriptor(byte Version, IReadOnlyList<Device> Devices);

	public sealed record Device(bool IsOutput, string Name, IReadOnlyList<Axis> Axes, int ButtonCount);

	public sealed record State(byte Version, IReadOnlyList<DeviceState> Devices);

	public sealed record DeviceState(IReadOnlyList<double> Axes, IReadOnlyList<bool> Buttons);

	public static FrameReader CreateReader(ReadOnlySpan<byte> frame) => new(frame);

	public static Descriptor ReadDescriptor(ReadOnlySpan<byte> frame)
	{
		if (CreateReader(frame).GetDescriptorFrameReader() is not { Success: true } readerOption)
		{
			throw new InvalidDataException(
				$"Not a descriptor frame: 0x{(frame.Length > 0 ? frame[0] : 0):X2}.");
		}

		var reader = readerOption.Value;
		if (reader.GetVersion() is not { Success: true } version ||
		    reader.GetDeviceCount() is not { Success: true } deviceCount)
		{
			throw new InvalidDataException("Descriptor frame is truncated.");
		}

		var cursor = reader.GetDeviceFrameReader().Value;
		var devices = new List<Device>(deviceCount.Value);
		for (var i = 0; i < deviceCount.Value; i++)
		{
			if (!cursor.MoveNext(out var info))
			{
				throw new InvalidDataException($"Descriptor frame is truncated at device {i}.");
			}

			var axes = new Axis[info.AxisCount];
			for (var a = 0; a < axes.Length; a++)
			{
				axes[a] = info.GetAxis(a);
			}

			devices.Add(new(info.IsOutput, info.GetName(), axes, info.ButtonCount));
		}

		return cursor.RemainingBytes == 0
			? new(version.Value, devices)
			: throw new InvalidDataException($"Descriptor has {cursor.RemainingBytes} trailing byte(s).");
	}

	public static State ReadState(ReadOnlySpan<byte> frame, Descriptor descriptor)
	{
		if (CreateReader(frame).GetStateFrameReader() is not { Success: true } readerOption)
		{
			throw new InvalidDataException(
				$"Not a state frame: 0x{(frame.Length > 0 ? frame[0] : 0):X2}.");
		}

		var reader = readerOption.Value;
		if (reader.GetVersion() is not { Success: true } version)
		{
			throw new InvalidDataException("State frame is truncated.");
		}

		var cursor = reader.GetDeviceStateReader();
		var devices = new List<DeviceState>(descriptor.Devices.Count);
		foreach (var device in descriptor.Devices)
		{
			if (!cursor.MoveNext((byte)device.Axes.Count, (byte)device.ButtonCount, out var info))
			{
				throw new InvalidDataException($"State frame is truncated at device {devices.Count}.");
			}

			var axes = new double[info.AxisCount];
			for (var a = 0; a < axes.Length; a++)
			{
				axes[a] = info.GetAxisValue(a);
			}

			var buttons = new bool[device.ButtonCount];
			for (var b = 0; b < buttons.Length; b++)
			{
				buttons[b] = info.IsButtonPressed(b + 1);
			}

			devices.Add(new(axes, buttons));
		}

		return cursor.RemainingBytes == 0
			? new(version.Value, devices)
			: throw new InvalidDataException($"State frame has {cursor.RemainingBytes} trailing byte(s).");
	}
}
