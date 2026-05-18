namespace SharpSticks.VJoy;

public static class VJoyDeviceBuilder
{
	extension(VJoyDevice)
	{
		public static VJoyDevice Open(
			uint deviceId,
			IReadOnlyList<ButtonRoute> buttonRoutes,
			IReadOnlyList<AxisRoute> axisRoutes,
			IReadOnlyCollection<int>? macroButtonNumbers = null) =>
			VJoyDeviceFactory.Instance.Open(deviceId, buttonRoutes, axisRoutes, macroButtonNumbers);
	}
}