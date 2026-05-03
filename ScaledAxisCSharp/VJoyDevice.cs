namespace ScaledAxisCSharp;

internal sealed class VJoyDevice : IDisposable
{
	private readonly Dictionary<VJoyAxis, AxisLimits> _AxisLimits;
	private readonly uint _DeviceId;
	private readonly Dictionary<VJoyAxis, int> _LastAxisValues = [];
	private readonly Dictionary<int, bool> _LastButtonValues = [];
	private bool _Disposed;

	private VJoyDevice(uint deviceId, Dictionary<VJoyAxis, AxisLimits> axisLimits)
	{
		_DeviceId = deviceId;
		_AxisLimits = axisLimits;
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

	public static VJoyDevice Open(
		int deviceId,
		IReadOnlyList<ButtonRoute> buttonRoutes,
		IReadOnlyList<AxisRoute> axisRoutes,
		IReadOnlyList<ScaledAxisRoute> scaledAxisRoutes)
	{
		if (deviceId < 1)
		{
			throw new InvalidOperationException("vJoy device ids are 1-based.");
		}

		VJoyNative.EnsureLoaded();

		if (!VJoyNative.VJoyEnabled())
		{
			throw new InvalidOperationException("vJoy is not enabled. Install and configure the vJoy driver first.");
		}

		var deviceIdUInt = (uint)deviceId;
		var status = VJoyNative.GetVJDStatus(deviceIdUInt);
		if (status == VjdStatus.Busy)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is already in use by another feeder.");
		}

		if (status == VjdStatus.Missing)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is not configured.");
		}

		if (!VJoyNative.AcquireVJD(deviceIdUInt))
		{
			throw new InvalidOperationException($"Failed to acquire vJoy device {deviceId}. Current status: {status}.");
		}

		if (!VJoyNative.ResetVJD(deviceIdUInt))
		{
			VJoyNative.RelinquishVJD(deviceIdUInt);
			throw new InvalidOperationException($"Failed to reset vJoy device {deviceId}.");
		}

		var axisLimits = new Dictionary<VJoyAxis, AxisLimits>();
		foreach (var axis in axisRoutes.Select(route => route.TargetAxis)
			         .Concat(scaledAxisRoutes.Select(route => route.TargetAxis))
			         .Distinct())
		{
			var hidUsage = (uint)axis;
			if (!VJoyNative.GetVJDAxisExist(deviceIdUInt, hidUsage))
			{
				VJoyNative.RelinquishVJD(deviceIdUInt);
				throw new InvalidOperationException($"Axis '{axis}' is not enabled on vJoy device {deviceId}.");
			}

			var min = 0;
			var max = 0;
			if (!VJoyNative.GetVJDAxisMin(deviceIdUInt, hidUsage, ref min) ||
			    !VJoyNative.GetVJDAxisMax(deviceIdUInt, hidUsage, ref max))
			{
				VJoyNative.RelinquishVJD(deviceIdUInt);
				throw new InvalidOperationException($"Failed reading limits for vJoy axis '{axis}'.");
			}

			axisLimits.Add(axis, new AxisLimits(min, max));
		}

		var buttonCount = VJoyNative.GetVJDButtonNumber(deviceIdUInt);
		foreach (var targetButton in buttonRoutes.Select(route => route.TargetButton).Distinct())
			if (targetButton > buttonCount)
			{
				VJoyNative.RelinquishVJD(deviceIdUInt);
				throw new InvalidOperationException(
					$"Button {targetButton} is not enabled on vJoy device {deviceId}. Device exposes {buttonCount} buttons.");
			}

		return new VJoyDevice(deviceIdUInt, axisLimits);
	}

	public void SetAxis(VJoyAxis axis, double normalizedValue)
	{
		ThrowIfDisposed();

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

		if (!VJoyNative.SetAxis(translated, _DeviceId, (uint)axis))
		{
			throw new InvalidOperationException($"Failed writing axis '{axis}' to vJoy device {_DeviceId}.");
		}

		_LastAxisValues[axis] = translated;
	}

	public void SetButton(int buttonNumber, bool pressed)
	{
		ThrowIfDisposed();

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

	private readonly record struct AxisLimits(int Min, int Max)
	{
		public int TranslateSigned(double value)
		{
			var centered = (value + 1.0) * 0.5;
			return Min + (int)Math.Round(centered * (Max - Min));
		}
	}
}