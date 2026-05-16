using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.OutputAbstractions;

public static class OutputExtensions
{
	public static OutputButtonBinding BindButton<T>(this T device, int sourceButton)
		where T : OutputDevice
	{
		return new(device.DeviceId, sourceButton);
	}

	public static OutputAxisBinding BindAxis<T>(this T device, Axis axis)
		where T : OutputDevice
	{
		return new(device.DeviceId, axis);
	}

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		OutputDevice outputDevice,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(outputDevice.DeviceId, binding.Axis, scale, offset, modifier);

	[OverloadResolutionPriority(3)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		OutputDevice outputDevice,
		RouteAxisOptions? options = null) =>
		binding.RouteToSameAxisOnOutput(outputDevice.DeviceId, options);
}