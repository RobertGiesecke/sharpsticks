using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using ScaledAxisCSharp.OutputAbstractions;

namespace ScaledAxisCSharp.VJoy;

public sealed class VJoyDevice : OutputDevice
{
	private readonly FrozenDictionary<PhysicalAxis, AxisLimits> _AxisLimits;
	private readonly Dictionary<PhysicalAxis, int> _LastAxisValues;
	private readonly Dictionary<int, bool> _LastButtonValues;

	public VJoyDevice(uint deviceId, Dictionary<PhysicalAxis, AxisLimits> axisLimits)
		: base(deviceId)
	{
		_AxisLimits = axisLimits.ToFrozenDictionary();
		_LastAxisValues = new Dictionary<PhysicalAxis, int>(axisLimits.Count);
		_LastButtonValues = new Dictionary<int, bool>(128);
	}


	public override void SetAxis(PhysicalAxis axis, double normalizedValue)
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

	public override void SetButton(int buttonNumber, bool pressed)
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
	}
}