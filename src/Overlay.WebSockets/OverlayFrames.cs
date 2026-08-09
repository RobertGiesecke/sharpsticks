using System.Collections.Immutable;
using Collections.Pooled;

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
	public sealed record Descriptor(byte Version, ImmutableArray<Device> Devices);

	public sealed record Device(bool IsOutput, string Name, ImmutableArray<Axis> Axes, int ButtonCount);

	public sealed record State(byte Version, ImmutableArray<DeviceState> Devices);

	public sealed record DeviceState(ImmutableArray<double> Axes, ImmutableArray<bool> Buttons);

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
		using var devices = new PooledList<Device>(deviceCount.Value, ClearMode.Always);
		for (var i = 0; i < deviceCount.Value; i++)
		{
			if (!cursor.MoveNext(out var info))
			{
				throw new InvalidDataException($"Descriptor frame is truncated at device {i}.");
			}

			var axes = ImmutableArray.CreateBuilder<Axis>(info.AxisCount);
			for (var a = 0; a < info.AxisCount; a++)
			{
				axes.Add(info.GetAxis(a));
			}

			devices.Add(new(info.IsOutput, info.GetName(), axes.ToImmutable(), info.ButtonCount));
		}

		return cursor.RemainingBytes == 0
			? new(version.Value, [.. devices.Span])
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
		using var devices = new PooledList<DeviceState>(descriptor.Devices.Length, ClearMode.Always);
		foreach (var device in descriptor.Devices)
		{
			if (!cursor.MoveNext((byte)device.Axes.Length, (byte)device.ButtonCount, out var info))
			{
				throw new InvalidDataException($"State frame is truncated at device {devices.Count}.");
			}

			var axes = ImmutableArray.CreateBuilder<double>(info.AxisCount);
			for (var a = 0; a < info.AxisCount; a++)
			{
				axes.Add(info.GetAxisValue(a));
			}

			var buttons = ImmutableArray.CreateBuilder<bool>(device.ButtonCount);
			for (var b = 0; b < device.ButtonCount; b++)
			{
				buttons.Add(info.IsButtonPressed(b + 1));
			}

			devices.Add(new(axes.ToImmutable(), buttons.ToImmutable()));
		}

		return cursor.RemainingBytes == 0
			? new(version.Value, [.. devices.Span])
			: throw new InvalidDataException($"State frame has {cursor.RemainingBytes} trailing byte(s).");
	}
}