using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.InputAbstractions;

public static class BindingExtensions
{
	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteToSameAxisOnVJoy(
		this AxisBinding binding,
		uint vJoyDeviceId,
		RouteAxisOptions? options = null) =>
		binding.RouteAxis(vJoyDeviceId, binding.Axis, options);

	public static AxisRoute RouteToSameAxisOnVJoy(
		this AxisBinding binding,
		uint vJoyDeviceId,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(vJoyDeviceId, binding.Axis, scale, offset, modifier);

	[OverloadResolutionPriority(1)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint vJoyDeviceId,
		PhysicalAxis vJoyAxis,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = binding,
			VJoyDeviceId = vJoyDeviceId,
			VJoyAxis = vJoyAxis,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint vJoyDeviceId,
		PhysicalAxis vJoyAxis,
		RouteAxisOptions? options = null) =>
		new()
		{
			Source = binding,
			VJoyDeviceId = vJoyDeviceId,
			VJoyAxis = vJoyAxis,
			Scale = options?.Scale ?? 1.0,
			Offset = options?.Offset ?? 0.0,
			Modifier = options?.Modifier,
		};

	public static ButtonRoute RouteButton(this ButtonBinding binding, uint vJoyDeviceId, int targetButton) =>
		new(binding, vJoyDeviceId, targetButton);

	public static IEnumerable<ButtonRoute> RouteButtonsToVJoy<TDevice>(
		this TDevice device,
		uint vJoyDeviceId,
		Func<TDevice, ButtonBinding, bool>? predicate = null)
		where TDevice : JoystickDevice
	{
		for (var i = 0; i < device.Capabilities.NumButtons; i++)
		{
			var binding = device.BindButton(i + 1);

			if (predicate?.Invoke(device, binding) is false)
			{
				continue;
			}

			yield return binding.RouteButton(vJoyDeviceId, binding.ButtonNumber);
		}
	}

	public static IEnumerable<AxisRoute> RouteAxesToVJoy<TDevice>(
		this TDevice device, uint vJoyDeviceId,
		Func<TDevice, AxisBinding, bool>? predicate = null,
		Func<TDevice, AxisBinding, RouteAxisOptions?>? optionsCallback = null)
		where TDevice : JoystickDevice
	{
		for (var i = 0; i < device.Capabilities.NumAxes; i++)
		{
			var axisType = device.PhysicalAxes[i];

			var axisBinding = device.BindAxis(axisType);
			if (predicate?.Invoke(device, axisBinding) is false)
			{
				continue;
			}

			if (optionsCallback?.Invoke(device, axisBinding) is not { } options)
			{
				options = new RouteAxisOptions();
			}

			yield return axisBinding.RouteToSameAxisOnVJoy(vJoyDeviceId, options);
		}
	}
}