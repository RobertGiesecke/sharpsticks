using Collections.Pooled;

namespace ScaledAxisCSharp.Testing;

/// <summary>
/// In-memory <see cref="OutputDevice"/> that captures every axis/button write.
/// Tests can inspect the latest value or the full ordered write log.
/// </summary>
/// <remarks>
/// When declared axes / button count are supplied (typically via
/// <see cref="FakeDeviceManager.AddOutputDevice"/>), writes to undeclared
/// axes or buttons beyond the count throw — mirroring the strictness of
/// <c>VJoyDevice</c> so tests catch routing typos. When declared axes is
/// <c>null</c>, the device is permissive (any axis/button accepted).
/// </remarks>
public sealed class FakeOutputDevice : OutputDevice, IFakeDevice
{
	private readonly PooledDictionary<Axis, double> _Axes;
	private readonly PooledDictionary<int, bool> _Buttons;
	private readonly PooledList<AxisWrite> _AxisWrites = [];
	private readonly PooledList<ButtonWrite> _ButtonWrites = [];
	private readonly ImmutableHashSet<Axis>? _DeclaredAxes;
	private readonly int? _ButtonCount;

	public FakeOutputDevice(
		uint deviceId,
		ImmutableHashSet<Axis>? declaredAxes = null,
		int? buttonCount = null)
		: base(deviceId)
	{
		_DeclaredAxes = declaredAxes;
		_ButtonCount = buttonCount;
		_Axes = new(declaredAxes?.Count ?? 0);
		_Buttons = new(buttonCount ?? 0);
	}

	public override void SetAxisValue(Axis axis, double normalizedValue)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (_DeclaredAxes is { } declared && !declared.Contains(axis))
		{
			throw new InvalidOperationException(
				$"Axis '{axis}' was not declared on fake output device {DeviceId}.");
		}

		var clamped = Math.Clamp(normalizedValue, -1.0, 1.0);
		_Axes[axis] = clamped;
		_AxisWrites.Add(new(axis, clamped));
	}

	public override void SetButtonState(int buttonNumber, bool pressed)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (_ButtonCount is { } count && buttonNumber > count)
		{
			throw new InvalidOperationException(
				$"Button {buttonNumber} exceeds declared count {count} on fake output device {DeviceId}.");
		}

		_Buttons[buttonNumber] = pressed;
		_ButtonWrites.Add(new(buttonNumber, pressed));
	}

	public double GetAxisValue(Axis axis) => _Axes.GetValueOrDefault(axis, 0.0);

	public bool GetButtonState(int buttonNumber) => _Buttons.GetValueOrDefault(buttonNumber, false);

	public IReadOnlyList<AxisWrite> AxisWrites => _AxisWrites;

	public IReadOnlyList<ButtonWrite> ButtonWrites => _ButtonWrites;

	public void ClearLog()
	{
		_AxisWrites.Clear();
		_ButtonWrites.Clear();
	}

	protected override void OnDispose()
	{
		_Axes.Dispose();
		_Buttons.Dispose();
		_AxisWrites.Dispose();
		_ButtonWrites.Dispose();
	}

	public readonly record struct AxisWrite(Axis Axis, double Value);

	public readonly record struct ButtonWrite(int ButtonNumber, bool Pressed);
}