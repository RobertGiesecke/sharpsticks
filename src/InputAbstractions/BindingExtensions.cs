using System.Runtime.CompilerServices;

namespace SharpSticks.InputAbstractions;

public static class BindingExtensions
{
	public static AxisBinding Invert(
		this AxisBinding binding) => binding with { Invert = !binding.Invert };

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

	public static GroupedSourceAxesWithModifiers WithModifier(
		this GroupedSourceAxes sourceAxes,
		IAxisModifier modifier) => new()
	{
		SourceAxes = [..sourceAxes.SourceAxes.WithModifier(modifier)],
	};

	public static IEnumerable<AxisBindingWithModifier> WithModifier(
		this IEnumerable<AxisBinding> sourceAxes,
		IAxisModifier modifier) =>
		sourceAxes.Select(a => new AxisBindingWithModifier
		{
			SourceAxis = a,
			Modifier = modifier,
		});

	public static AxisBindingWithModifier WithModifier(
		this AxisBinding sourceAxis,
		IAxisModifier modifier) =>
		new()
		{
			SourceAxis = sourceAxis,
			Modifier = modifier,
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

	public static AbsoluteRelativeAxisRoute RouteAbsoluteRelative(
		this AxisBinding binding,
		AbsoluteRelativeAxisOptions options)
	{
		return new()
		{
			Binding = binding,
			Options = options,
		};
	}

	public static ButtonToTargetRoute RouteButton(this ButtonBinding binding, uint outputDeviceId, int targetButton) =>
		binding.RouteTo(new OutputButtonBinding(outputDeviceId, targetButton));

	public static ButtonToTargetRoute RouteTo(this ButtonBinding binding, OutputButtonBinding outputBinding) =>
		new() { Source = binding, Target = outputBinding };

	public readonly record struct AxisZoneOptions()
	{
		public bool IncludeMax { get; init; } = true;
		public AxisZoneTriggerMode Mode { get; init; } = AxisZoneTriggerMode.Hold;
		public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(50);
	}

	public static AxisZoneRoute RouteWhenInRange(
		this AxisBinding axis,
		double min,
		double max,
		ButtonTarget output,
		AxisZoneOptions? options = null)
	{
		var o = options ?? new();
		return new()
		{
			Source = axis,
			Target = output,
			Min = min,
			Max = max,
			IncludeMax = o.IncludeMax,
			Mode = o.Mode,
			PulseDuration = o.PulseDuration,
		};
	}

	public static MultiAxesToButtonRoute RouteWhenInRange(
		this GroupedSourceAxes axes,
		double min,
		double max,
		ButtonTarget output,
		AxisZoneOptions? options = null)
	{
		var o = options ?? new();
		return new()
		{
			Sources = axes.SourceAxes,
			Target = output,
			Min = min,
			Max = max,
			IncludeMax = o.IncludeMax,
			Mode = o.Mode,
			PulseDuration = o.PulseDuration,
		};
	}

	public readonly record struct AxisZone(double Min, double Max, ButtonTarget Output);

	public static IEnumerable<AxisZoneRoute> RouteZones(
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
				Target = zone.Output,
				Min = zone.Min,
				Max = zone.Max,
				IncludeMax = o.IncludeMax,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			};
		}
	}

	public static ImmutableArray<IRoute> SplitIntoButtons(
		this AxisBinding axis,
		ImmutableArray<ButtonTarget> outputs,
		AxisZoneOptions? options = null)
	{
		if (outputs.IsDefaultOrEmpty)
		{
			throw new ArgumentException("At least one output button is required.", nameof(outputs));
		}

		var o = options ?? new();
		var (lo, hi) = axis.Mode == AxisMode.Unsigned ? (0.0, 1.0) : (-1.0, 1.0);
		var step = (hi - lo) / outputs.Length;
		var builder = ImmutableArray.CreateBuilder<IRoute>(outputs.Length);
		for (var i = 0; i < outputs.Length; i++)
		{
			var isLast = i == outputs.Length - 1;

			// Every target kind is just an AxisZoneRoute now; the ButtonTarget decides the
			// sink (level for vJoy/key/mouse, pulse-on-entry for scroll).
			builder.Add(new AxisZoneRoute
			{
				Source = axis,
				Target = outputs[i],
				Min = lo + step * i,
				Max = isLast ? hi : lo + step * (i + 1),
				IncludeMax = isLast,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			});
		}

		return builder.MoveToImmutable();
	}

	public static IEnumerable<MultiAxesToButtonRoute> RouteZones(
		this GroupedSourceAxes axes,
		IEnumerable<AxisZone> zones,
		AxisZoneOptions? options = null)
	{
		var o = options ?? new();
		foreach (var zone in zones)
		{
			yield return new()
			{
				Sources = axes.SourceAxes,
				Target = zone.Output,
				Min = zone.Min,
				Max = zone.Max,
				IncludeMax = o.IncludeMax,
				Mode = o.Mode,
				PulseDuration = o.PulseDuration,
			};
		}
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

	public static IEnumerable<ButtonToTargetRoute> RouteButtonsToOutput<TDevice>(
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