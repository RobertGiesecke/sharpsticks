namespace ScaledAxisCSharp.OutputAbstractions;

public interface IOutputDeviceFactory
{
	OutputDevice Open(
		uint deviceId,
		IReadOnlyList<ButtonRoute> buttonRoutes,
		IReadOnlyList<AxisRoute> axisRoutes);
}