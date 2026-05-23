using Collections.Pooled;

namespace SharpSticks.VJoy;

public static class VJoyDeviceBuilder
{
	extension(VJoyDevice)
	{
		/// Open a single vJoy device. Convenience wrapper around the batched factory
		/// API; returns the one created device. Caller owns disposal.
		public static VJoyDevice Open(
			uint deviceId,
			IReadOnlyList<ButtonRoute> buttonRoutes,
			IReadOnlyList<AxisRoute> axisRoutes,
			IReadOnlyCollection<int>? macroButtonNumbers = null)
		{
			using var opened = VJoyDeviceFactory.Instance.Open(
				new[] { new OutputDeviceRequest(deviceId, buttonRoutes, axisRoutes, macroButtonNumbers ?? []) });
			return opened[0];
		}
	}
}