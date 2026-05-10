namespace ScaledAxisCSharp.VJoy;

public static class VJoyDeviceBuilder
{
	extension(VJoyDevice)
	{
		public static VJoyDevice Open(
			uint deviceId,
			IReadOnlyList<ButtonRoute> buttonRoutes,
			IReadOnlyList<AxisRoute> axisRoutes) =>
			VJoyDeviceFactory.Instance.Open(deviceId, buttonRoutes, axisRoutes);
	}
}