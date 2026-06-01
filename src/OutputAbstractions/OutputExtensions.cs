using System.Runtime.CompilerServices;

namespace SharpSticks.OutputAbstractions;

public static class OutputExtensions
{
	public static OutputButtonBinding BindButton<T>(this T device, int sourceButton)
		where T : IOutputDevice
	{
		return new(device.DeviceId, sourceButton);
	}

	public static OutputAxisBinding BindAxis<T>(this T device, Axis axis)
		where T : IOutputDevice
	{
		return new(device.DeviceId, axis);
	}

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		IOutputDevice outputDevice,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(outputDevice.DeviceId, binding.Axis, scale, offset, modifier);

	[OverloadResolutionPriority(3)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		IOutputDevice outputDevice,
		RouteAxisOptions? options = null) =>
		binding.RouteToSameAxisOnOutput(outputDevice.DeviceId, options);

	[OverloadResolutionPriority(2)]
	public static ImmutableArray<AxisRoute> RouteToSameAxesOnOutput(
		this GroupedSourceAxes bindings,
		IOutputDevice outputDevice,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		bindings.RouteToSameAxesOnOutput(outputDevice.DeviceId, scale, offset, modifier);

	[OverloadResolutionPriority(3)]
	public static ImmutableArray<AxisRoute> RouteToSameAxesOnOutput(
		this GroupedSourceAxes bindings,
		IOutputDevice outputDevice,
		RouteAxisOptions? options = null) =>
		bindings.RouteToSameAxesOnOutput(outputDevice.DeviceId, options);

	public static IEnumerable<ButtonRoute> RouteToOutput<TOutputDevice>(
		this ImmutableArray<ButtonBinding> sourceButtons,
		TOutputDevice outputDevice,
		Func<ButtonBinding, bool>? predicate = null)
		where TOutputDevice : IOutputDevice
	{
		var outputDeviceId = outputDevice.DeviceId;

		using var result = new PooledList<ButtonRoute>(sourceButtons.Length);
		var newSpan = result.AddSpan(sourceButtons.Length);

		for (var i = 0; i < sourceButtons.Length; i++)
		{
			var binding = sourceButtons[i];

			if (predicate?.Invoke(binding) is false)
			{
				continue;
			}

			newSpan[i] = binding.RouteTo(new(outputDeviceId, binding.ButtonNumber));
		}

		return [..newSpan];
	}

	public static IEnumerable<AxisRoute> RouteToOutput<TOutputDevice>(
		this ImmutableArray<AxisBinding> sourceAxes,
		TOutputDevice outputDevice,
		Func<AxisBinding, bool>? predicate = null,
		Func<AxisBinding, RouteAxisOptions?>? optionsCallback = null)
		where TOutputDevice : IOutputDevice
	{
		using var result = new PooledList<AxisRoute>(sourceAxes.Length);
		var newSpan = result.AddSpan(sourceAxes.Length);
		var outputIndex = 0;
		for (var index = 0; index < sourceAxes.Length; index++)
		{
			var axisBinding = sourceAxes[index];
			if (predicate?.Invoke(axisBinding) is false)
			{
				continue;
			}

			if (optionsCallback?.Invoke(axisBinding) is not { } options)
			{
				options = new();
			}

			newSpan[outputIndex++] = axisBinding.RouteToSameAxisOnOutput(outputDevice.DeviceId, options);
		}

		return [..newSpan.Slice(0, outputIndex)];
	}
}