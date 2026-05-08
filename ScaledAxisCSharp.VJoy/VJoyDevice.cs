using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.VJoy;

public readonly record struct AxisLimits(int Min, int Max)
{
	public int TranslateSigned(double value)
	{
		var centered = (value + 1.0) * 0.5;
		return Min + (int)Math.Round(centered * (Max - Min));
	}
}

public sealed class VJoyDevice : IDisposable
{
	private readonly FrozenDictionary<PhysicalAxis, AxisLimits> _AxisLimits;
	private readonly uint _DeviceId;
	private readonly Dictionary<PhysicalAxis, int> _LastAxisValues;
	private readonly Dictionary<int, bool> _LastButtonValues;
	private bool _Disposed;
	private bool _Frozen;

	public VJoyDevice(uint deviceId, Dictionary<PhysicalAxis, AxisLimits> axisLimits)
	{
		_DeviceId = deviceId;
		_AxisLimits = axisLimits.ToFrozenDictionary();
		_LastAxisValues = new Dictionary<PhysicalAxis, int>(axisLimits.Count);
		_LastButtonValues = new Dictionary<int, bool>(128);
	}

	public void Freeze()
	{
		_Frozen = true;
	}

	public void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		VJoyNative.RelinquishVJD(_DeviceId);
	}


	public void SetAxis(PhysicalAxis axis, double normalizedValue)
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

		if (!VJoyNative.SetAxis(translated, _DeviceId, axis.GetVJoyAxisId()))
		{
			throw new InvalidOperationException($"Failed writing axis '{axis}' to vJoy device {_DeviceId}.");
		}

		_LastAxisValues[axis] = translated;
	}

	public void SetButton(int buttonNumber, bool pressed)
	{
		ThrowIfDisposed();
		ThrowIfFrozen();

		if (_LastButtonValues.TryGetValue(buttonNumber, out var lastState) && lastState == pressed)
		{
			return;
		}

		if (!VJoyNative.SetBtn(pressed, _DeviceId, (uint)buttonNumber))
		{
			throw new InvalidOperationException($"Failed writing button {buttonNumber} to vJoy device {_DeviceId}.");
		}

		_LastButtonValues[buttonNumber] = pressed;
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_Disposed, this);
	}

	private void ThrowIfFrozen([CallerMemberName] string? memberName = null)
	{
		if (!_Frozen)
		{
			return;
		}

		throw new InvalidOperationException($"Cannot modify {memberName} of vJoy device after it has been frozen.");
	}
}