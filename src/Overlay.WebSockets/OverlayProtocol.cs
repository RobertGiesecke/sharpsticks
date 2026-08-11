using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// Serializes the current device state into the binary frames the joyviz
/// overlay consumes over WebSocket. The byte grammar itself lives in the
/// frame primitives (<see cref="DescriptorFrameWriter"/> /
/// <see cref="StateFrameWriter"/> for writing, <see cref="FrameReader"/> and
/// friends for reading) — this type only binds them to live
/// <see cref="JoystickDevice"/>s: the descriptor is built once (the device
/// set is fixed for the process lifetime) and the state buffer is allocated
/// once and reused every frame, so the per-frame path never allocates.
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
	private readonly ImmutableArray<TInputDevice> _Devices;
	private readonly AxisBinding[][] _AxisBindings;
	private readonly int[] _ButtonCounts;
	private readonly byte _Version;
	private readonly byte[] _StateBuffer;

	public byte[] Descriptor { get; }

	public OverlayProtocol(ImmutableArray<TInputDevice> devices, byte version)
	{
		if (devices.Length > byte.MaxValue)
		{
			throw new ArgumentException(
				$"The overlay protocol carries at most {byte.MaxValue} devices; got {devices.Length}.",
				nameof(devices));
		}

		_Devices = devices;
		_Version = version;
		_AxisBindings = new AxisBinding[devices.Length][];
		_ButtonCounts = new int[devices.Length];

		var stateSize = StateFrameWriter.HeaderSize;
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
			// Clamped ONCE: descriptor and state frames must derive their
			// button-byte counts from the same number or readers desync.
			_ButtonCounts[i] = Math.Min((int)device.Capabilities.NumButtons, byte.MaxValue);
			stateSize += StateFrameWriter.MeasureDevice(axes.Length, _ButtonCounts[i]);
		}

		_StateBuffer = new byte[stateSize];
		Descriptor = BuildDescriptor();
	}

	private byte[] BuildDescriptor()
	{
		// Size the descriptor exactly, then write it through the frame writer.
		var size = DescriptorFrameWriter.HeaderSize;
		using var nameBytesOwner = MemoryPool<IMemoryOwner<byte>>.Shared.Rent(_Devices.Length);
		using var nameLengthOwner = MemoryPool<int>.Shared.Rent(_Devices.Length);


		var utf8 = Encoding.UTF8;
		for (var i = 0; i < _Devices.Length; i++)
		{
			var name = _Devices[i].Name ?? "";
			var byteCount = utf8.GetByteCount(name);
			using var deferBytesOwner = MemoryPool<byte>.Shared.Rent(byteCount).Defer();
			var usedByteCount = utf8.GetBytes(name, deferBytesOwner.Value.Memory.Span);

			if (usedByteCount > byte.MaxValue)
			{
				usedByteCount = byte.MaxValue;
			}

			size += DescriptorFrameWriter.MeasureDevice(usedByteCount, _Devices[i].PhysicalAxes.Length);
			nameLengthOwner.Memory.Span[i] = usedByteCount;
			nameBytesOwner.Memory.Span[i] = deferBytesOwner.GetAndSkipDispose();
		}

		using var deferBuffer =  MemoryPool<byte>.Shared.Rent(size).Defer();
		var buffer = deferBuffer.Value;

		var writer = new DescriptorFrameWriter(buffer.Memory.Span, _Version, (byte)_Devices.Length);
		for (var i = 0; i < _Devices.Length; i++)
		{
			var device = _Devices[i];
			var usedByteCount = nameLengthOwner.Memory.Span[i];

			using var usedBytesOwner = nameBytesOwner.Memory.Span[i].Defer();

			var usedBytes = usedBytesOwner.Value.Memory.Span[..usedByteCount];
			if (!writer.TryWriteDevice(
				    device.IsVirtualOutputMirror,
				    usedBytes,
				    device.PhysicalAxes.AsSpan(),
				    (byte)_ButtonCounts[i]))
			{
				throw new InvalidOperationException("Descriptor buffer was sized incorrectly.");
			}
		}

		return deferBuffer.GetAndSkipDispose().Memory.ToArray();
	}

	/// <summary>
	/// Fills the reused state buffer from the latest per-device states and returns the slice
	/// to broadcast. <paramref name="states"/> is indexed like the device array; a null entry
	/// (device unreadable this frame) serializes as centered axes and released buttons.
	/// </summary>
	public ReadOnlySpan<byte> WriteState(JoystickState?[] states)
	{
		scoped var writer = new StateFrameWriter(_StateBuffer, _Version);

		for (var i = 0; i < _Devices.Length; i++)
		{
			var device = _Devices[i];
			var bindings = _AxisBindings[i];
			var buttonCount = _ButtonCounts[i];
			if (!writer.TryBeginDevice((byte)bindings.Length, (byte)buttonCount, out scoped var deviceWriter))
			{
				throw new InvalidOperationException("State buffer was sized incorrectly.");
			}

			// A null state leaves the freshly-zeroed slot as-is: centered + released.
			if (states[i] is not { } state)
			{
				continue;
			}

			for (var a = 0; a < bindings.Length; a++)
			{
				deviceWriter.WriteAxisValue(a, device.ReadNormalizedAxisValue(state, bindings[a]));
			}

			for (var b = 1; b <= buttonCount; b++)
			{
				if (state.IsButtonPressed(b))
				{
					deviceWriter.SetButton(b);
				}
			}
		}

		return _StateBuffer.AsSpan(0, writer.BytesWritten);
	}
}
