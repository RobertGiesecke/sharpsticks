namespace ScaledAxisCSharp.OutputAbstractions;

public interface IOutputDeviceFactory
{
	OutputDevice Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes);
}