namespace ScaledAxisCSharp.VJoy;

public sealed class VJoyDeviceFactory : IOutputDeviceFactory
{
	public static VJoyDeviceFactory Instance { get; } = new();

	OutputDevice IOutputDeviceFactory.Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes) => Open(deviceId, buttonRoutes, axisRoutes);

	public VJoyDevice Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes)
	{
		if (deviceId < 1)
		{
			throw new InvalidOperationException("vJoy device ids are 1-based.");
		}

		VJoyNative.EnsureLoaded();

		if (!VJoyNative.VJoyEnabled())
		{
			throw new InvalidOperationException(
				"vJoy is not enabled. Install and configure the vJoy driver first.");
		}

		var status = VJoyNative.GetVJDStatus(deviceId);
		if (status == VjdStatus.Busy)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is already in use by another feeder.");
		}

		if (status == VjdStatus.Missing)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is not configured.");
		}

		if (!VJoyNative.AcquireVJD(deviceId))
		{
			throw new InvalidOperationException(
				$"Failed to acquire vJoy device {deviceId}. Current status: {status}.");
		}

		if (!VJoyNative.ResetVJD(deviceId))
		{
			VJoyNative.RelinquishVJD(deviceId);
			throw new InvalidOperationException($"Failed to reset vJoy device {deviceId}.");
		}

		var axisLimits = new Dictionary<PhysicalAxis, AxisLimits>();
		foreach (var axis in axisRoutes.Select(route => route.OutputAxis)
			         .Distinct())
		{
			var hidUsage = axis.GetVJoyAxisId();
			if (!VJoyNative.GetVJDAxisExist(deviceId, hidUsage))
			{
				VJoyNative.RelinquishVJD(deviceId);
				throw new InvalidOperationException($"Axis '{axis}' is not enabled on vJoy device {deviceId}.");
			}

			var min = 0;
			var max = 0;
			if (!VJoyNative.GetVJDAxisMin(deviceId, hidUsage, ref min) ||
			    !VJoyNative.GetVJDAxisMax(deviceId, hidUsage, ref max))
			{
				VJoyNative.RelinquishVJD(deviceId);
				throw new InvalidOperationException($"Failed reading limits for vJoy axis '{axis}'.");
			}

			axisLimits.Add(axis, new AxisLimits(min, max));
		}

		var buttonCount = VJoyNative.GetVJDButtonNumber(deviceId);
		foreach (var targetButton in buttonRoutes.Select(route => route.TargetButton).Distinct())
		{
			if (targetButton > buttonCount)
			{
				VJoyNative.RelinquishVJD(deviceId);
				throw new InvalidOperationException(
					$"Button {targetButton} is not enabled on vJoy device {deviceId}. Device exposes {buttonCount} buttons.");
			}
		}

		return new VJoyDevice(deviceId, axisLimits);
	}
}