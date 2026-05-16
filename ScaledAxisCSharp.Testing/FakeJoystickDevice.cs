using Collections.Pooled;

namespace ScaledAxisCSharp.Testing;

/// <summary>
/// In-memory <see cref="JoystickDevice"/> for deterministic tests. Axis values
/// are stored as already-normalized doubles (the conventional [-1, 1] for
/// signed bindings, [0, 1] for unsigned) and surfaced as-is through
/// <see cref="ReadAxisDebugSample"/> — so tests don't have to reason about
/// device ranges, deadzones, or decoder kinds.
/// </summary>
public sealed class FakeJoystickDevice : JoystickDevice, IFakeDevice
{
	private readonly PooledDictionary<Axis, double> _NormalizedAxes = new();
	private readonly bool[] _Buttons;
	private readonly AutoResetEvent _DataAvailable;
	private bool _Disposed;

	[SetsRequiredMembers]
	public FakeJoystickDevice(
		int deviceId,
		string name,
		ImmutableArray<Axis> axes,
		int buttonCount = 32,
		string? instanceName = null)
	{
		_DataAvailable = new AutoResetEvent(initialState: false);
		_Buttons = new bool[Math.Max(buttonCount, 1)];

		DeviceId = deviceId;
		Name = name;
		InstanceName = instanceName ?? name;
		Capabilities = new JoystickCapabilities((uint)axes.Length, (uint)buttonCount, 0);
		PhysicalAxes = axes;
		DataAvailable = _DataAvailable;
	}

	/// <summary>
	/// Sets the normalized value reported for the given axis and signals
	/// <see cref="JoystickDevice.DataAvailable"/> so a <c>Run</c> loop wakes up.
	/// Tests that drive <see cref="IOutputRuntimeContext.ProcessFrame"/>
	/// directly don't need the signal — they can ignore it.
	/// </summary>
	public void SetAxisValue(Axis axis, double normalizedValue)
	{
		_NormalizedAxes[axis] = normalizedValue;
		_DataAvailable.Set();
	}
	
	public double GetAxisValue(Axis axis) => _NormalizedAxes.GetValueOrDefault(axis);

	public void PressButton(int buttonNumber) => SetButtonState(buttonNumber, pressed: true);

	public void ReleaseButton(int buttonNumber) => SetButtonState(buttonNumber, pressed: false);

	public void SetButtonState(int buttonNumber, bool pressed)
	{
		ValidateButtonNumber(buttonNumber);
		_Buttons[buttonNumber - 1] = pressed;
		_DataAvailable.Set();
	}

	public bool GetButtonState(int buttonNumber)
	{
		ValidateButtonNumber(buttonNumber);
		return _Buttons[buttonNumber - 1];
	}

	private void ValidateButtonNumber(int buttonNumber)
	{
		if (buttonNumber < 1 || buttonNumber > _Buttons.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(buttonNumber));
		}
	}

	public override bool TryReadState(out JoystickState state, out string? error)
	{
		ulong low = 0;
		ulong high = 0;
		for (var i = 0; i < _Buttons.Length; i++)
		{
			if (!_Buttons[i])
			{
				continue;
			}

			if (i < 64)
			{
				low |= 1UL << i;
			}
			else
			{
				high |= 1UL << (i - 64);
			}
		}

		state = new JoystickState(
			ToInt(Axis.X), ToInt(Axis.Y), ToInt(Axis.Z),
			ToInt(Axis.Rx), ToInt(Axis.Ry), ToInt(Axis.Rz),
			ToInt(Axis.Slider1), ToInt(Axis.Slider2),
			low,
			high);
		error = null;
		return true;

		int ToInt(Axis a) =>
			_NormalizedAxes.TryGetValue(a, out var v)
				? (int)Math.Round(Math.Clamp(v, -1.0, 1.0) * 32767.0)
				: 0;
	}

	public override double ReadNormalizedAxisValue(in JoystickState state, AxisBinding binding) =>
		ReadAxisDebugSample(state, binding).NormalizedValue;

	public override AxisDebugSample ReadAxisDebugSample(in JoystickState state, AxisBinding binding)
	{
		var value = _NormalizedAxes.GetValueOrDefault(binding.Axis, 0.0);
		value = binding.Mode == AxisMode.Unsigned
			? Math.Clamp(value, 0.0, 1.0)
			: Math.Clamp(value, -1.0, 1.0);

		if (binding.Invert)
		{
			value = binding.Mode == AxisMode.Signed ? -value : 1.0 - value;
		}

		return new AxisDebugSample(
			state.GetAxisValue(binding.Axis),
			RangeMin: -32767,
			RangeMax: 32767,
			value,
			binding.Mode == AxisMode.Unsigned ? AxisDecoderKind.Unsigned : AxisDecoderKind.NativeSigned);
	}

	public override void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		_NormalizedAxes.Dispose();
		_DataAvailable.Dispose();
	}
}
