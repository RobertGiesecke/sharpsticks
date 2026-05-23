using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SharpSticks.LinuxInput;

/// evdev-backed <see cref="JoystickDevice"/>. Direct libc P/Invoke; no userspace
/// library between us and the kernel. NativeAOT-compatible (LibraryImport, no reflection).
public sealed class LinuxInputJoystickDevice : JoystickDevice
{
	private readonly int _Fd;
	private readonly FrozenDictionary<Axis, AxisRange> _AxisRanges;
	private readonly FrozenDictionary<ushort, int> _ButtonCodeToIndex;
	private readonly CancellationTokenSource _PollLoopCts = new();
	private readonly Thread _PollThread;
	private MutableState _CurrentState;
	private byte[] _ReadBuffer = new byte[LinuxInputEvent.Size * 64];
	private bool _Disposed;

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
		PhysicalAxes = info.Axes;
		Capabilities = new(
			(uint)info.Axes.Length,
			(uint)Math.Min(info.ButtonCodes.Length, 128),
			NumPovs: 0);
		DataAvailable = dataAvailable;
		_CurrentState = default;

		_PollThread = new(PollLoop) { IsBackground = true, Name = $"LinuxInput poll {info.EventPath}" };
		_PollThread.Start(new PollContext(_Fd, dataAvailable, _PollLoopCts.Token));
	}

	public override void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		_PollLoopCts.Cancel();
		LinuxInputNative.Close(_Fd);
		_PollLoopCts.Dispose();
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
			var bytesRead = LinuxInputNative.Read(_Fd, ref MemoryMarshal.GetReference(buffer), (nuint)buffer.Length);
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
			case LinuxInputEventCodes.EvAbs:
				ApplyAbsEvent(ev.Code, ev.Value);
				break;
			case LinuxInputEventCodes.EvKey:
				ApplyKeyEvent(ev.Code, ev.Value);
				break;
		}
	}

	private void ApplyAbsEvent(ushort code, int value)
	{
		switch (code)
		{
			case LinuxInputEventCodes.AbsX: _CurrentState.X = value; break;
			case LinuxInputEventCodes.AbsY: _CurrentState.Y = value; break;
			case LinuxInputEventCodes.AbsZ: _CurrentState.Z = value; break;
			case LinuxInputEventCodes.AbsRx: _CurrentState.Rx = value; break;
			case LinuxInputEventCodes.AbsRy: _CurrentState.Ry = value; break;
			case LinuxInputEventCodes.AbsRz: _CurrentState.Rz = value; break;
			case LinuxInputEventCodes.AbsThrottle: _CurrentState.Slider1 = value; break;
			case LinuxInputEventCodes.AbsRudder: _CurrentState.Slider2 = value; break;
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

	public static PooledList<LinuxInputJoystickDevice> EnumerateConnected()
	{
		var infos = LinuxInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
		var devices = new PooledList<LinuxInputJoystickDevice>(infos.Length);
		try
		{
			foreach (var info in infos)
			{
				var device = OpenDevice(info);
				if (device is not null)
				{
					devices.Add(device);
				}
			}

			return devices;
		}
		catch
		{
			DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}

	private static LinuxInputJoystickDevice? OpenDevice(LinuxInputDeviceInfo info)
	{
		var fd = LinuxInputNative.Open(info.EventPath,
			LinuxInputEventCodes.OReadOnly | LinuxInputEventCodes.ONonBlock | LinuxInputEventCodes.OCloseOnExec);
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
			return new LinuxInputJoystickDevice(
				info,
				fd,
				rangesBuilder.ToFrozenDictionary(),
				buttonMap.ToFrozenDictionary(),
				dataAvailable);
		}
		catch
		{
			dataAvailable?.Dispose();
			LinuxInputNative.Close(fd);
			throw;
		}
	}

	private static bool TryGetAbsRange(int fd, Axis axis, out AxisRange range)
	{
		var absCode = axis switch
		{
			Axis.X => LinuxInputEventCodes.AbsX,
			Axis.Y => LinuxInputEventCodes.AbsY,
			Axis.Z => LinuxInputEventCodes.AbsZ,
			Axis.Rx => LinuxInputEventCodes.AbsRx,
			Axis.Ry => LinuxInputEventCodes.AbsRy,
			Axis.Rz => LinuxInputEventCodes.AbsRz,
			Axis.Slider1 => LinuxInputEventCodes.AbsThrottle,
			Axis.Slider2 => LinuxInputEventCodes.AbsRudder,
			_ => (ushort)0xffff,
		};

		var info = default(LinuxAbsInfo);
		if (absCode == 0xffff || LinuxInputNative.IoctlAbsInfo(fd, LinuxInputEventCodes.EviocgAbs(absCode), ref info) < 0)
		{
			range = default;
			return false;
		}

		range = new(info.Minimum, info.Maximum);
		return true;
	}

	private static void DisposeAll(IEnumerable<LinuxInputJoystickDevice> devices)
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}

	private static void PollLoop(object? rawContext)
	{
		var context = (PollContext)rawContext!;
		var pollFd = new LinuxPollFd { Fd = context.Fd, Events = LinuxInputEventCodes.PollIn };

		while (!context.CancellationToken.IsCancellationRequested)
		{
			pollFd.Revents = 0;
			var result = LinuxInputNative.Poll(ref pollFd, 1, 250);
			if (result > 0 && (pollFd.Revents & LinuxInputEventCodes.PollIn) != 0)
			{
				try
				{
					context.DataAvailable.Set();
				}
				catch (ObjectDisposedException)
				{
					return;
				}
			}
		}
	}

	private sealed record PollContext(int Fd, AutoResetEvent DataAvailable, CancellationToken CancellationToken);

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
