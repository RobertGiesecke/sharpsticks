using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SharpSticks.LinuxInput;

/// evdev-backed <see cref="JoystickDevice"/>. Direct libc P/Invoke; no userspace
/// library between us and the kernel. NativeAOT-compatible (LibraryImport, no reflection).
public sealed class LinuxInputJoystickDevice : JoystickDevice, IJoystickDeviceWithFactory<LinuxInputJoystickDevice>
{
	private readonly int _Fd;
	private readonly FrozenDictionary<Axis, AxisRange> _AxisRanges;
	private readonly FrozenDictionary<ushort, int> _ButtonCodeToIndex;
	private MutableState _CurrentState;
	private byte[] _ReadBuffer = new byte[LinuxInputEvent.Size * 64];
	private bool _Disposed;

	public static LinuxInputJoystickDeviceFactory Factory => LinuxInputJoystickDeviceFactory.Instance;
	static IJoystickDeviceFactory<LinuxInputJoystickDevice> IJoystickDeviceWithFactory<LinuxInputJoystickDevice>.Factory => Factory;

	[SetsRequiredMembers]
	private LinuxInputJoystickDevice(
		LinuxInputDeviceInfo info,
		int fd,
		FrozenDictionary<Axis, AxisRange> axisRanges,
		FrozenDictionary<ushort, int> buttonCodeToIndex,
		AutoResetEvent dataAvailable)
	{
		_Fd = fd;
		_AxisRanges = axisRanges;
		_ButtonCodeToIndex = buttonCodeToIndex;
		DeviceId = info.DeviceId;
		Name = info.ProductName;
		InstanceName = info.InstanceName;
		InstanceGuid = info.InstanceGuid;
		ProductGuid = info.ProductGuid;
		PhysicalAxes = info.Axes;
		Capabilities = new(
			(uint)info.Axes.Length,
			(uint)Math.Min(info.ButtonCodes.Length, 128),
			NumPovs: 0
		);
		DataAvailable = dataAvailable;
		_CurrentState = default;

		LinuxInputEventLoop.Register(_Fd, dataAvailable);
	}

	public override void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		LinuxInputEventLoop.Unregister(_Fd);
		LinuxLibc.Close(_Fd);
		((AutoResetEvent)DataAvailable).Dispose();
	}

	public override bool TryReadState(out JoystickState state, out string? error)
	{
		DrainEvents();
		state = _CurrentState.ToImmutable();
		error = null;
		return true;
	}

	public override double ReadNormalizedAxisValue(in JoystickState state, AxisBinding binding)
	{
		return ReadAxisDebugSample(state, binding).NormalizedValue;
	}

	public override AxisDebugSample ReadAxisDebugSample(in JoystickState state, AxisBinding binding)
	{
		var rawValue = state.GetAxisValue(binding.Axis);
		if (!_AxisRanges.TryGetValue(binding.Axis, out var range))
		{
			range = new(-1, 1);
		}

		var normalized = Normalize(rawValue, range, binding.Mode, binding.Invert, binding.Deadzone, out var decoder);
		return new(rawValue, range.Min, range.Max, normalized, decoder);
	}

	private void DrainEvents()
	{
		Span<byte> buffer = _ReadBuffer.AsSpan();
		while (true)
		{
			var bytesRead = LinuxLibc.Read(_Fd, ref MemoryMarshal.GetReference(buffer), (nuint)buffer.Length);
			if (bytesRead <= 0)
			{
				return;
			}

			var events = MemoryMarshal.Cast<byte, LinuxInputEvent>(buffer[..(int)bytesRead]);
			foreach (ref readonly var ev in events)
			{
				ApplyEvent(in ev);
			}
		}
	}

	private void ApplyEvent(in LinuxInputEvent ev)
	{
		switch (ev.Type)
		{
			case EvType.Abs:
				ApplyAbsEvent(ev.Code, ev.Value);
				break;
			case EvType.Key:
				ApplyKeyEvent(ev.Code, ev.Value);
				break;
		}
	}

	private void ApplyAbsEvent(ushort code, int value)
	{
		switch (code)
		{
			case LinuxEventCodes.AbsX: _CurrentState.X = value; break;
			case LinuxEventCodes.AbsY: _CurrentState.Y = value; break;
			case LinuxEventCodes.AbsZ: _CurrentState.Z = value; break;
			case LinuxEventCodes.AbsRx: _CurrentState.Rx = value; break;
			case LinuxEventCodes.AbsRy: _CurrentState.Ry = value; break;
			case LinuxEventCodes.AbsRz: _CurrentState.Rz = value; break;
			case LinuxEventCodes.AbsThrottle: _CurrentState.Slider1 = value; break;
			case LinuxEventCodes.AbsRudder: _CurrentState.Slider2 = value; break;
		}
	}

	private void ApplyKeyEvent(ushort code, int value)
	{
		if (!_ButtonCodeToIndex.TryGetValue(code, out var index))
		{
			return;
		}

		var pressed = value != 0;
		if (index < 64)
		{
			var bit = 1UL << index;
			if (pressed)
			{
				_CurrentState.ButtonBitsLow |= bit;
			}
			else
			{
				_CurrentState.ButtonBitsLow &= ~bit;
			}
		}
		else if (index < 128)
		{
			var bit = 1UL << (index - 64);
			if (pressed)
			{
				_CurrentState.ButtonBitsHigh |= bit;
			}
			else
			{
				_CurrentState.ButtonBitsHigh &= ~bit;
			}
		}
	}

	private static double Normalize(
		int rawValue,
		AxisRange range,
		AxisMode mode,
		bool invert,
		double deadzone,
		out AxisDecoderKind decoder)
	{
		if (range.Max <= range.Min)
		{
			decoder = AxisDecoderKind.Unknown;
			return 0.0;
		}

		double normalized;
		if (mode == AxisMode.Unsigned)
		{
			decoder = AxisDecoderKind.Unsigned;
			normalized = (rawValue - range.Min) / (double)(range.Max - range.Min);
			normalized = Math.Clamp(normalized, 0.0, 1.0);
		}
		else
		{
			decoder = AxisDecoderKind.NativeSigned;
			normalized = (rawValue - range.Min) / (double)(range.Max - range.Min) * 2.0 - 1.0;
			normalized = Math.Clamp(normalized, -1.0, 1.0);
		}

		if (invert)
		{
			normalized = mode == AxisMode.Signed ? -normalized : 1.0 - normalized;
		}

		deadzone = Math.Clamp(deadzone, 0.0, 0.99);
		if (deadzone > 0.0)
		{
			if (mode == AxisMode.Signed)
			{
				var magnitude = Math.Abs(normalized);
				normalized = magnitude <= deadzone
					? 0.0
					: Math.CopySign((magnitude - deadzone) / (1.0 - deadzone), normalized);
			}
			else
			{
				normalized = normalized <= deadzone ? 0.0 : (normalized - deadzone) / (1.0 - deadzone);
			}
		}

		return normalized;
	}

	public static PooledList<LinuxInputJoystickDevice> EnumerateConnected() =>
		LinuxInputJoystickDeviceFactory.Instance.EnumerateConnectedInputDevices();

	internal static LinuxInputJoystickDevice? OpenDevice(LinuxInputDeviceInfo info)
	{
		var fd = LinuxLibc.Open(
			info.EventPath,
			OpenFlags.ReadOnly | OpenFlags.NonBlock | OpenFlags.CloseOnExec
		);
		if (fd < 0)
		{
			return null;
		}

		AutoResetEvent? dataAvailable = null;
		try
		{
			var rangesBuilder = new Dictionary<Axis, AxisRange>(info.Axes.Length);
			foreach (var axis in info.Axes)
			{
				if (TryGetAbsRange(fd, axis, out var range))
				{
					rangesBuilder[axis] = range;
				}
			}

			var buttonMap = new Dictionary<ushort, int>(info.ButtonCodes.Length);
			for (var i = 0; i < info.ButtonCodes.Length && i < 128; i++)
			{
				buttonMap[info.ButtonCodes[i]] = i;
			}

			dataAvailable = new(false);
			return new(
				info,
				fd,
				rangesBuilder.ToFrozenDictionary(),
				buttonMap.ToFrozenDictionary(),
				dataAvailable
			);
		}
		catch
		{
			dataAvailable?.Dispose();
			LinuxLibc.Close(fd);
			throw;
		}
	}

	private static bool TryGetAbsRange(int fd, Axis axis, out AxisRange range)
	{
		var absCode = axis switch
		{
			Axis.X => LinuxEventCodes.AbsX,
			Axis.Y => LinuxEventCodes.AbsY,
			Axis.Z => LinuxEventCodes.AbsZ,
			Axis.Rx => LinuxEventCodes.AbsRx,
			Axis.Ry => LinuxEventCodes.AbsRy,
			Axis.Rz => LinuxEventCodes.AbsRz,
			Axis.Slider1 => LinuxEventCodes.AbsThrottle,
			Axis.Slider2 => LinuxEventCodes.AbsRudder,
			_ => (ushort)0xffff,
		};

		var info = default(LinuxAbsInfo);
		if (absCode == 0xffff || LinuxLibc.IoctlAbsInfo(fd, EvdevIoctls.EviocgAbs(absCode), ref info) < 0)
		{
			range = default;
			return false;
		}

		range = new(info.Minimum, info.Maximum);
		return true;
	}

	internal static void DisposeAll(PooledList<LinuxInputJoystickDevice> devices)
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}

	private struct MutableState
	{
		public int X;
		public int Y;
		public int Z;
		public int Rx;
		public int Ry;
		public int Rz;
		public int Slider1;
		public int Slider2;
		public ulong ButtonBitsLow;
		public ulong ButtonBitsHigh;

		public readonly JoystickState ToImmutable() =>
			new(X, Y, Z, Rx, Ry, Rz, Slider1, Slider2, ButtonBitsLow, ButtonBitsHigh);
	}

	internal readonly record struct AxisRange(int Min, int Max);
}