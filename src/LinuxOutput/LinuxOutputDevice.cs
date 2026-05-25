namespace SharpSticks.LinuxOutput;

/// uinput-backed <see cref="OutputDevice"/>. The factory has already configured the device's
/// axes and buttons via ioctl and called UI_DEV_CREATE; this class just <c>write()</c>s
/// <c>input_event</c> bursts to push state changes through the kernel.
public sealed class LinuxOutputDevice : OutputDevice, IOutputDeviceWithFactory<LinuxOutputDevice>
{
	internal const int AxisRangeMin = -32767;
	internal const int AxisRangeMax = 32767;

	private readonly int _Fd;
	private readonly FrozenDictionary<Axis, ushort> _AxisCodes;
	private readonly FrozenDictionary<int, ushort> _ButtonCodes;
	private readonly PooledDictionary<Axis, int> _LastAxisValues;
	private readonly PooledDictionary<int, bool> _LastButtonValues;

	public static LinuxOutputDeviceFactory Factory => LinuxOutputDeviceFactory.Instance;
	static IOutputDeviceFactory<LinuxOutputDevice> IOutputDeviceWithFactory<LinuxOutputDevice>.Factory => Factory;

	internal LinuxOutputDevice(
		uint deviceId,
		int fd,
		FrozenDictionary<Axis, ushort> axisCodes,
		FrozenDictionary<int, ushort> buttonCodes)
		: base(deviceId)
	{
		_Fd = fd;
		_AxisCodes = axisCodes;
		_ButtonCodes = buttonCodes;
		_LastAxisValues = new(axisCodes.Count);
		_LastButtonValues = new(buttonCodes.Count);
	}

	public override void SetAxisValue(Axis axis, double normalizedValue)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (!_AxisCodes.TryGetValue(axis, out var code))
		{
			throw new InvalidOperationException($"Axis '{axis}' was not declared on this output device.");
		}

		normalizedValue = Math.Clamp(normalizedValue, -1.0, 1.0);
		var translated = (int)Math.Round(
			(normalizedValue + 1.0) / 2.0 * (AxisRangeMax - AxisRangeMin) + AxisRangeMin);

		if (_LastAxisValues.TryGetValue(axis, out var last) && last == translated)
		{
			return;
		}

		WriteBurst(LinuxEventCodes.EvAbs, code, translated);
		_LastAxisValues[axis] = translated;
	}

	public override void SetButtonState(int buttonNumber, bool pressed)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (!_ButtonCodes.TryGetValue(buttonNumber, out var code))
		{
			throw new InvalidOperationException(
				$"Button {buttonNumber} was not declared on this output device.");
		}

		if (_LastButtonValues.TryGetValue(buttonNumber, out var last) && last == pressed)
		{
			return;
		}

		WriteBurst(LinuxEventCodes.EvKey, code, pressed ? 1 : 0);
		_LastButtonValues[buttonNumber] = pressed;
	}

	protected override void OnDispose()
	{
		LinuxLibc.IoctlNoArg(_Fd, LinuxUinput.UiDevDestroy);
		LinuxLibc.Close(_Fd);
		_LastAxisValues.Dispose();
		_LastButtonValues.Dispose();
	}

	private void WriteBurst(ushort type, ushort code, int value)
	{
		// Two events in one write(): the change + the SYN_REPORT terminator so the kernel
		// publishes immediately. Stackalloc'd; no allocation.
		Span<LinuxInputEvent> burst = stackalloc LinuxInputEvent[2];
		burst[0] = new LinuxInputEvent { Type = type, Code = code, Value = value };
		burst[1] = new LinuxInputEvent
		{
			Type = LinuxEventCodes.EvSyn,
			Code = LinuxEventCodes.SynReport,
			Value = 0,
		};

		var bytes = MemoryMarshal.AsBytes(burst);
		var written = LinuxLibc.Write(_Fd, ref MemoryMarshal.GetReference(bytes), (nuint)bytes.Length);
		if (written != bytes.Length)
		{
			throw new InvalidOperationException(
				$"uinput write returned {written}, expected {bytes.Length}, errno {LinuxLibc.LastError}.");
		}
	}
}
