using Collections.Pooled;
using SharpSticks.InputSynthesis.Windows;

namespace SharpSticks.VJoy;

public sealed class VJoyDevice : OutputDevice, IOutputDeviceWithFactory<VJoyDevice>
{
	private readonly FrozenDictionary<Axis, AxisLimits> _AxisLimits;
	private readonly PooledDictionary<Axis, int> _LastAxisValues;
	private readonly PooledDictionary<int, bool> _LastButtonValues;

	public static VJoyDeviceFactory Factory => VJoyDeviceFactory.Instance;
	static IOutputDeviceFactory<VJoyDevice> IOutputDeviceWithFactory<VJoyDevice>.Factory => Factory;

	// vJoy is the Windows output marker, so the Windows SendInput synthesizer is
	// the platform default for key/mouse macro output.
	public static IInputSynthesizer? DefaultInputSynthesizer => WindowsInputSynthesizer.Instance;

	public VJoyDevice(uint deviceId, FrozenDictionary<Axis, AxisLimits> axisLimits)
		: base(deviceId)
	{
		_AxisLimits = axisLimits;
		_LastAxisValues = new(axisLimits.Count);
		_LastButtonValues = new(128);
	}


	public override void SetAxisValue(Axis axis, double normalizedValue)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (!_AxisLimits.TryGetValue(axis, out var limits))
		{
			throw new InvalidOperationException($"Axis '{axis}' was not initialized.");
		}

		normalizedValue = Math.Clamp(normalizedValue, -1.0, 1.0);
		var translated = limits.TranslateSigned(normalizedValue);

		if (_LastAxisValues.TryGetValue(axis, out var lastValue) && lastValue == translated)
		{
			return;
		}

		if (!VJoyNative.SetAxis(translated, DeviceId, axis.GetVJoyAxisId()))
		{
			throw new InvalidOperationException($"Failed writing axis '{axis}' to vJoy device {DeviceId}.");
		}

		_LastAxisValues[axis] = translated;
	}

	public override void SetButtonState(int buttonNumber, bool pressed)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (_LastButtonValues.TryGetValue(buttonNumber, out var lastState) && lastState == pressed)
		{
			return;
		}

		if (!VJoyNative.SetBtn(pressed, DeviceId, (uint)buttonNumber))
		{
			throw new InvalidOperationException($"Failed writing button {buttonNumber} to vJoy device {DeviceId}.");
		}

		_LastButtonValues[buttonNumber] = pressed;
	}

	protected override void OnDispose()
	{
		VJoyNative.RelinquishVJD(DeviceId);
		_LastAxisValues.Dispose();
		_LastButtonValues.Dispose();
	}
}