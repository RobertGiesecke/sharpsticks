namespace ScaledAxisCSharp.Config;

public static class VJoyDeviceBuilder
{
	extension(VJoyDevice)
	{
		public static VJoyDevice Open(
			int deviceId,
			IReadOnlyList<ButtonRoute> buttonRoutes,
			IReadOnlyList<AxisRoute> axisRoutes)
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
				throw new InvalidOperationException(
					$"Failed to acquire vJoy device {deviceId}. Current status: {status}.");
			}

			if (!VJoyNative.ResetVJD(deviceIdUInt))
			{
				VJoyNative.RelinquishVJD(deviceIdUInt);
				throw new InvalidOperationException($"Failed to reset vJoy device {deviceId}.");
			}

			var axisLimits = new Dictionary<PhysicalAxis, AxisLimits>();
			foreach (var axis in axisRoutes.Select(route => route.VJoyAxis)
				         .Distinct())
			{
				var hidUsage = axis.GetVJoyAxisId();
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
			{
				if (targetButton > buttonCount)
				{
					VJoyNative.RelinquishVJD(deviceIdUInt);
					throw new InvalidOperationException(
						$"Button {targetButton} is not enabled on vJoy device {deviceId}. Device exposes {buttonCount} buttons.");
				}
			}

			return new VJoyDevice(deviceIdUInt, axisLimits);
		}
	}
}