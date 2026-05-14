using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.InputAbstractions;

public static class BindingExtensions
{
	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		uint outputDeviceId,
		RouteAxisOptions? options = null) =>
		binding.RouteAxis(outputDeviceId, binding.Axis, options);

	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		uint outputDeviceId,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(outputDeviceId, binding.Axis, scale, offset, modifier);

	[OverloadResolutionPriority(1)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint outputDeviceId,
		PhysicalAxis outputAxis,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = binding,
			OutputDeviceId = outputDeviceId,
			OutputAxis = outputAxis,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint outputDeviceId,
		PhysicalAxis outputAxis,
		RouteAxisOptions? options = null) =>
		new()
		{
			Source = binding,
			OutputDeviceId = outputDeviceId,
			OutputAxis = outputAxis,
			Scale = options?.Scale ?? 1.0,
			Offset = options?.Offset ?? 0.0,
			Modifier = options?.Modifier,
		};

	public static ImmutableArray<AxisRoute> RouteAbsoluteRelative(
		this AxisBinding binding,
		AbsoluteRelativeAxisOptions options)
	{
		if (options.OutputDeviceId < 1)
		{
			throw new InvalidOperationException("Output device ids are 1-based.");
		}

		if (options.IncreaseAxis == options.DecreaseAxis)
		{
			throw new InvalidOperationException("IncreaseAxis and DecreaseAxis must be different.");
		}

		var sharedState = new AbsoluteRelativeAxisModifier.SharedState(options);
		var increaseModifier = new AbsoluteRelativeAxisModifier(sharedState,
			AbsoluteRelativeAxisModifier.RelativeDirection.Increase,
			options.IncreaseRestPosition);
		var decreaseModifier = new AbsoluteRelativeAxisModifier(sharedState,
			AbsoluteRelativeAxisModifier.RelativeDirection.Decrease,
			options.DecreaseRestPosition);

		return
		[
			binding.RouteAxis(
				options.OutputDeviceId,
				options.IncreaseAxis,
				modifier: increaseModifier),
			binding.RouteAxis(
				options.OutputDeviceId,
				options.DecreaseAxis,
				modifier: decreaseModifier),
		];
	}

	public static ButtonRoute RouteButton(this ButtonBinding binding, uint outputDeviceId, int targetButton) =>
		new(binding, outputDeviceId, targetButton);

	public static IEnumerable<ButtonRoute> RouteButtonsToOutput<TDevice>(
		this TDevice device,
		uint outputDeviceId,
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

			yield return binding.RouteButton(outputDeviceId, binding.ButtonNumber);
		}
	}

	public static IEnumerable<AxisRoute> RouteAxesToOutput<TDevice>(
		this TDevice device, uint outputDeviceId,
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

			yield return axisBinding.RouteToSameAxisOnOutput(outputDeviceId, options);
		}
	}
}