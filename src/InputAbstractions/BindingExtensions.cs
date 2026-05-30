using System.Runtime.CompilerServices;

namespace SharpSticks.InputAbstractions;

public static class BindingExtensions
{
	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		uint outputDeviceId,
		RouteAxisOptions? options = null) =>
		binding.RouteAxis(outputDeviceId, binding.Axis, options);

	public static GroupedSourceAxes GroupWith(
		this AxisBinding binding,
		params ReadOnlySpan<AxisBinding> otherAxes) =>
		new()
		{
			SourceAxes = [binding, ..otherAxes],
		};

	public static ImmutableArray<AxisRoute> RouteToSameAxesOnOutput(
		this GroupedSourceAxes bindings,
		uint outputDeviceId,
		RouteAxisOptions? options = null) =>
	[
		..bindings.SourceAxes.Distinct().Select(b => b.RouteAxis(outputDeviceId, b.Axis, options))
	];


	public static ImmutableArray<AxisRoute> RouteToSameAxesOnOutput(
		this GroupedSourceAxes bindings,
		uint outputDeviceId,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
	[
		..bindings.SourceAxes.Distinct().Select(b =>
			b.RouteToSameAxisOnOutput(outputDeviceId, scale: scale, offset: offset, modifier: modifier))
	];


	public static AxisRoute RouteToSameAxisOnOutput(
		this AxisBinding binding,
		uint outputDeviceId,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		binding.RouteAxis(outputDeviceId, binding.Axis, scale, offset, modifier);

	[OverloadResolutionPriority(1)]
	public static AxisRoute RouteTo(
		this AxisBinding binding,
		OutputAxisBinding outputBinding,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		new()
		{
			Source = binding,
			OutputBinding = outputBinding,
			Scale = scale,
			Offset = offset,
			Modifier = modifier,
		};

	[OverloadResolutionPriority(1)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint outputDeviceId,
		Axis outputAxis,
		double scale = 1.0,
		double offset = 0.0,
		IAxisModifier? modifier = null) =>
		RouteTo(
			binding,
			new(outputDeviceId, outputAxis),
			scale,
			offset,
			modifier);

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteTo(
		this AxisBinding binding,
		OutputAxisBinding outputBinding,
		RouteAxisOptions? options = null)
	{
		var (scale, offset) = ResolveScaleOffset(options);
		return new()
		{
			Source = binding,
			OutputBinding = outputBinding,
			Scale = scale,
			Offset = offset,
			Modifier = options?.Modifier,
		};
	}

	private static (double Scale, double Offset) ResolveScaleOffset(RouteAxisOptions? options)
	{
		if (options is not { } o)
		{
			return (RouteAxisOptions.DefaultScale, 0.0);
		}

		return o.Invert ? (-o.Scale, -o.Offset) : (o.Scale, o.Offset);
	}

	private static (double Scale, double Offset) ResolveScaleOffset(RouteAxisOptions options) =>
		options.Invert ? (-options.Scale, -options.Offset) : (options.Scale, options.Offset);

	[OverloadResolutionPriority(2)]
	public static AxisRoute RouteAxis(
		this AxisBinding binding,
		uint outputDeviceId,
		Axis outputAxis,
		RouteAxisOptions? options = null) =>
		RouteTo(
			binding,
			new(outputDeviceId, outputAxis),
			options);

	public static AxisRoute MergeWith(
		this AxisBinding first,
		AxisBinding second,
		MergeAxesOptions options)
	{
		var (firstScale, firstOffset) = ResolveScaleOffset(options.First);
		var (secondScale, secondOffset) = ResolveScaleOffset(options.Second);
		return new()
		{
			Source = first,
			OutputBinding = options.OutputBinding,
			Scale = firstScale,
			Offset = firstOffset,
			Modifier = new MergeAxesModifier(
				second,
				secondScale,
				secondOffset,
				options.Second?.Modifier,
				options.First?.Modifier,
				options.Mode),
		};
	}

	public static ImmutableArray<AxisRoute> RouteAbsoluteRelative(
		this AxisBinding binding,
		AbsoluteRelativeAxisOptions options)
	{
		if (options.IncreaseAxis.OutputDeviceId < 1)
		{
			throw new InvalidOperationException("Output device ids are 1-based.");
		}

		if (options.DecreaseAxis.OutputDeviceId < 1)
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
			binding.RouteTo(
				options.IncreaseAxis,
				modifier: increaseModifier),
			binding.RouteTo(
				options.DecreaseAxis,
				modifier: decreaseModifier),
		];
	}

	public static ButtonRoute RouteButton(this ButtonBinding binding, uint outputDeviceId, int targetButton) =>
		RouteTo(binding, new(outputDeviceId, targetButton));

	public static ButtonRoute RouteTo(this ButtonBinding binding, OutputButtonBinding outputBinding) =>
		new(binding, outputBinding);

	public readonly record struct AxisZoneOptions()
	{
		public bool IncludeMax { get; init; } = true;
		public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;
		public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);
	}

	public static AxisToButtonRoute RouteWhenInRange(
		this AxisBinding axis,
		double min,
		double max,
		OutputButtonBinding output,
		AxisZoneOptions? options = null)
	{
		var o = options ?? new();
		return new()
		{
			Source = axis,
			OutputBinding = output,
			Min = min,
			Max = max,
			IncludeMax = o.IncludeMax,
			Mode = o.Mode,
			PulseDuration = o.PulseDuration,
		};
	}

	public readonly record struct AxisZone(double Min, double Max, OutputButtonBinding Output);

	public static IEnumerable<AxisToButtonRoute> RouteZones(
		this AxisBinding axis,
		IEnumerable<AxisZone> zones,
		AxisZoneOptions? options = null)
	{
		var o = options ?? new();
		foreach (var zone in zones)
		{
			yield return new()
			{
				Source = axis,
				OutputBinding = zone.Output,
				Min = zone.Min,
				Max = zone.Max,
				IncludeMax = o.IncludeMax,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			};
		}
	}

	public static ImmutableArray<AxisToButtonRoute> SplitIntoButtons(
		this AxisBinding axis,
		ImmutableArray<OutputButtonBinding> outputs,
		AxisZoneOptions? options = null)
	{
		if (outputs.IsDefaultOrEmpty)
		{
			throw new ArgumentException("At least one output button is required.", nameof(outputs));
		}

		var o = options ?? new();
		var (lo, hi) = axis.Mode == AxisMode.Unsigned ? (0.0, 1.0) : (-1.0, 1.0);
		var step = (hi - lo) / outputs.Length;
		var builder = ImmutableArray.CreateBuilder<AxisToButtonRoute>(outputs.Length);
		for (var i = 0; i < outputs.Length; i++)
		{
			var isLast = i == outputs.Length - 1;
			builder.Add(new()
			{
				Source = axis,
				OutputBinding = outputs[i],
				Min = lo + step * i,
				Max = isLast ? hi : lo + step * (i + 1),
				IncludeMax = isLast,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			});
		}

		return builder.MoveToImmutable();
	}


	public readonly record struct ComplexRouteOptions()
	{
		public ImmutableArray<IMacroAction> OnPress { get; init; } = [];
		public ImmutableArray<IMacroAction> OnRelease { get; init; } = [];
		public MacroReentry? Reentry { get; init; }
	}

	public static ButtonMacroRoute ComplexRoute(this ButtonBinding binding, ComplexRouteOptions options) =>
		new()
		{
			Binding = binding,
			Reentry = options.Reentry ?? ButtonMacroRoute.DefaultReentry,
			OnPress = options.OnPress,
			OnRelease = options.OnRelease,
		};

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

			yield return binding.RouteTo(new(outputDeviceId, binding.ButtonNumber));
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
				options = new();
			}

			yield return axisBinding.RouteToSameAxisOnOutput(outputDeviceId, options);
		}
	}
}