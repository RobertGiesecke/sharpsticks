using System.Runtime.CompilerServices;

namespace SharpSticks.OutputAbstractions;

//public sealed class Runtime : IOutputRuntimeContext, IDisposable
public sealed class Runtime<TInputDevice, TOutputDevice> : IOutputRuntimeContext<TInputDevice, TOutputDevice>
	where TInputDevice : JoystickDevice
	where TOutputDevice : OutputDevice
{
	public string Name { get; }
	private readonly DebugLogger? _DebugLogger;
	private readonly ImmutableArray<OutputAxisRoute> _AxisRoutes;
	private readonly ImmutableArray<OutputButtonWithBindings> _ButtonRoutes;
	private readonly ImmutableArray<OutputAxisToButtonRoute> _AxisToButtonRoutes;
	private long? _AxisZoneNextDeadlineTicks;
	private readonly FrozenDictionary<OutputButtonBinding, OutputButtonWithBindings> _ButtonRoutesByBinding;
	private readonly ImmutableArray<TInputDevice> _Devices;
	private readonly ImmutableArray<TOutputDevice> _OutputDevices;
	private readonly MacroEngine? _Macros;
	private readonly ITimeSource _Time;

	public ImmutableArray<TOutputDevice> OutputDevices => _OutputDevices;
	public FrozenDictionary<int, TInputDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<TInputDevice> Devices => _Devices;
	public ITimeSource TimeSource => _Time;

	private record struct OutputButtonState
	{
		public int Pressers;
		public int Suppressors;
		public bool WasRouteAssertingLastFrame;
	}

	/// <summary>
	/// One entry per distinct output button (route- or macro-targeted, or both).
	/// Tracks current assertions across all sources via <see cref="Pressers"/>
	/// and macro release-overrides via <see cref="Suppressors"/>. The button is
	/// held when <c>Pressers &gt; 0 &amp;&amp; Suppressors == 0</c>.
	/// </summary>
	private sealed class OutputButtonWithBindings
	{
		public required TOutputDevice OutputDevice { get; init; }
		public required int TargetButton { get; init; }
		public required OutputButtonBinding TargetBinding { get; init; }
		public required ImmutableArray<ButtonBindingWithDeviceId> Bindings { get; init; }

		private OutputButtonState _OutputButtonState;
		public int Pressers => _OutputButtonState.Pressers;
		public int Suppressors => _OutputButtonState.Suppressors;
		public bool WasRouteAssertingLastFrame => _OutputButtonState.WasRouteAssertingLastFrame;

		public void SetWasRouteAssertingLastFrame(bool value)
		{
			ref var outputButtonState = ref _OutputButtonState;
			outputButtonState.WasRouteAssertingLastFrame = value;
		}

		public static void IncrementPressers(ref OutputButtonState outputButtonState) => outputButtonState.Pressers++;

		public static void IncrementSuppressors(ref OutputButtonState outputButtonState) =>
			outputButtonState.Suppressors++;

		public static void DecrementSuppressors(ref OutputButtonState outputButtonState) =>
			outputButtonState.Suppressors--;

		public static void DecrementPressers(ref OutputButtonState outputButtonState) => outputButtonState.Pressers--;

		public void IncrementPressers() => IncrementPressers(ref _OutputButtonState);
		public void IncrementSuppressors() => IncrementSuppressors(ref _OutputButtonState);
		public void DecrementSuppressors() => DecrementSuppressors(ref _OutputButtonState);
		public void DecrementPressers() => DecrementPressers(ref _OutputButtonState);
	}

	private sealed class ButtonBindingWithDeviceId
	{
		public required ButtonBinding ButtonBinding { get; init; }
		public required int SourceDeviceIndex { get; init; }
	}

	private sealed class OutputAxisRoute
	{
		public required TOutputDevice OutputDevice { get; init; }
		public required int SourceDeviceIndex { get; init; }
		public required TInputDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required Axis OutputAxis { get; init; }
		public required double Scale { get; init; }
		public required double Offset { get; init; }
		public required IAxisModifier? Modifier { get; init; }
		public required IRuntimeAxisModifier? RuntimeModifier { get; init; }
	}

	private sealed class OutputAxisToButtonRoute
	{
		public required int SourceDeviceIndex { get; init; }
		public required TInputDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required double Min { get; init; }
		public required double Max { get; init; }
		public required bool IncludeMax { get; init; }
		public required AxisZoneTriggerMode Mode { get; init; }
		public required long PulseDurationTicks { get; init; }
		public required OutputButtonWithBindings Output { get; init; }

		public bool IsAsserting;
		public long PulseDeadlineTicks;
		public bool PulseCooldown;
	}

	public Runtime(
		string name,
		DebugLogger? debugLogger,
		PooledDictionary<int, TInputDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		ImmutableArray<ButtonMacroRoute> macroRoutes,
		ImmutableArray<AxisToButtonRoute> axisToButtonRoutes,
		ImmutableArray<OutputButtonBinding> auxiliaryOutputButtons,
		ITimeSource timeSource,
		ImmutableArray<TOutputDevice> outputDevices)
	{
		Name = name;
		_DebugLogger = debugLogger;
		_Time = timeSource;
		_Devices = [..devices.Values];
		DevicesById = devices.ToFrozenDictionary();
		{
			using var indexesById = new PooledDictionary<int, int>(_Devices.Length);
			for (var index = 0; index < _Devices.Length; index++)
			{
				var joystickDevice = _Devices[index];
				indexesById.Add(joystickDevice.DeviceId, index);
			}

			DeviceIndexesById = indexesById.ToFrozenDictionary();
		}
		using var outputDeviceIndexes = outputDevices
			.Select((device, index) => new { device.DeviceId, Index = index })
			.ToPooledDictionary(t => t.DeviceId, t => t.Index);

		// Macro / axis-zone output buttons get an OutputButtonWithBindings entry
		// too so ApplyButtons can OR their held sets in for them.
		using var allOutputButtons = new PooledSet<OutputButtonBinding>();

		using var mergedObjectsLookup = new PooledDictionary<IMergeableObject, IMergeableObject>();
		var mergeObjectContext = new MergeObjectContext
		{
			MergedObjects = mergedObjectsLookup,
		};

		var mergedButtonRoutes = buttonRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedAuxiliaryOutputButtons = auxiliaryOutputButtons.MergeOrGetAll(mergeObjectContext);
		var mergedMacroRoutes = macroRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedAxisRoutes = axisRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedAxisToButtonRoutes = axisToButtonRoutes.MergeOrGetAll(mergeObjectContext);

		foreach (var route in mergedButtonRoutes)
		{
			allOutputButtons.Add(route.OutputBinding);
		}


		foreach (var binding in mergedAuxiliaryOutputButtons)
		{
			allOutputButtons.Add(binding);
		}

		using var bindingsByOutput = mergedButtonRoutes
			.GroupBy(t => t.OutputBinding)
			.ToPooledDictionary(g => g.Key, g => g.ToArray());

		_ButtonRoutes =
		[
			..allOutputButtons.Select(binding =>
			{
				// ReSharper disable once AccessToDisposedClosure
				var sources = bindingsByOutput.GetValueOrDefault(binding, []);
				return new OutputButtonWithBindings
				{
					// ReSharper disable once AccessToDisposedClosure
					OutputDevice = outputDevices[outputDeviceIndexes[binding.OutputDeviceId]],
					TargetButton = binding.ButtonNumber,
					TargetBinding = binding,
					Bindings =
					[
						..sources.Select(t => t.Binding)
							.Distinct()
							.Select(t => new ButtonBindingWithDeviceId
							{
								ButtonBinding = t,
								SourceDeviceIndex = DeviceIndexesById[t.DeviceId],
							}),
					],
				};
			}),
		];
		_AxisRoutes =
		[
			..mergedAxisRoutes.Select(route => new OutputAxisRoute
			{
				// ReSharper disable once AccessToDisposedClosure
				OutputDevice = outputDevices[outputDeviceIndexes[route.OutputBinding.OutputDeviceId]],
				SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
				SourceDevice = DevicesById[route.Source.DeviceId],
				Source = route.Source,
				OutputAxis = route.OutputBinding.Axis,
				Scale = route.Scale,
				Offset = route.Offset,
				Modifier = route.Modifier,
				RuntimeModifier = route.Modifier?.CreateModifierRuntimeContext(this),
			})
		];
		_OutputDevices = outputDevices;
		_ButtonRoutesByBinding = _ButtonRoutes.ToFrozenDictionary(r => r.TargetBinding);
		_AxisToButtonRoutes =
		[
			..mergedAxisToButtonRoutes.Select(route =>
			{
				var durationSeconds = route.PulseDuration.TotalSeconds;
				var durationTicks = (long)(durationSeconds * timeSource.Frequency);
				return new OutputAxisToButtonRoute
				{
					SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
					SourceDevice = DevicesById[route.Source.DeviceId],
					Source = route.Source,
					Min = route.Min,
					Max = route.Max,
					IncludeMax = route.IncludeMax,
					Mode = route.Mode,
					PulseDurationTicks = durationTicks,
					Output = _ButtonRoutesByBinding[route.OutputBinding],
				};
			})
		];
		_CurrentStates = new JoystickState?[DevicesById.Count];
		_LastReportedReadFailure = new();
		_Macros = mergedMacroRoutes.IsEmpty
			? null
			: new MacroEngine(
				mergedMacroRoutes,
				DeviceIndexesById,
				timeSource,
				IncrementPressers,
				DecrementPressers,
				IncrementSuppressors,
				DecrementSuppressors);
	}

	private void IncrementPressers(OutputButtonBinding b) => _ButtonRoutesByBinding[b].IncrementPressers();
	private void DecrementPressers(OutputButtonBinding b) => _ButtonRoutesByBinding[b].DecrementPressers();
	private void IncrementSuppressors(OutputButtonBinding b) => _ButtonRoutesByBinding[b].IncrementSuppressors();
	private void DecrementSuppressors(OutputButtonBinding b) => _ButtonRoutesByBinding[b].DecrementSuppressors();

	private readonly JoystickState?[] _CurrentStates;
	private readonly PooledSet<int> _LastReportedReadFailure;

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		debugLogger ??= _DebugLogger;
		try
		{
			LogStartup(debugLogger);

			var waitHandles = DevicesById.Values
				.Select(d => d.DataAvailable)
				.Append(cancellationToken.WaitHandle)
				.ToArray();
			var cancelIndex = waitHandles.Length - 1;

			while (true)
			{
				var timeout = ComputeWaitTimeoutMs();
				if (WaitHandle.WaitAny(waitHandles, timeout) == cancelIndex)
				{
					break;
				}

				ProcessFrame(debugLogger);
			}
		}
		finally
		{
			foreach (var outputDevice in _OutputDevices)
			{
				outputDevice.Dispose();
			}
		}
	}

	private int ComputeWaitTimeoutMs()
	{
		long? deadline = null;
		if (_Macros?.NextDeadlineTicks is { } macroDeadline)
		{
			deadline = macroDeadline;
		}

		if (_AxisZoneNextDeadlineTicks is { } zoneDeadline &&
		    (deadline is null || zoneDeadline < deadline))
		{
			deadline = zoneDeadline;
		}

		if (deadline is null)
		{
			return Timeout.Infinite;
		}

		var remaining = deadline.Value - _Time.GetTimestamp();
		if (remaining <= 0)
		{
			return 0;
		}

		var ms = remaining * 1000 / _Time.Frequency;
		return ms > int.MaxValue ? int.MaxValue : (int)ms;
	}

	public void ProcessFrame(DebugLogger? debugLogger = null)
	{
		debugLogger ??= _DebugLogger;

		var currentStates = _CurrentStates;
		currentStates.AsSpan().Clear();

		for (var deviceIndex = 0; deviceIndex < _Devices.Length; deviceIndex++)
		{
			var device = _Devices[deviceIndex];
			if (device.TryReadState(out var state, out var error))
			{
				currentStates[deviceIndex] = state;
				_LastReportedReadFailure.Remove(device.DeviceId);
			}
			else if (_LastReportedReadFailure.Add(device.DeviceId) && error is not null)
			{
				Console.Error.WriteLine(error);
			}
		}

		var shouldLogNow = debugLogger?.ShouldLogNow() is true;

		_Macros?.Step(currentStates);
		StepAxisZones(currentStates);
		ApplyButtons(currentStates, shouldLogNow ? debugLogger : null);
		ApplyAxes(currentStates, shouldLogNow ? debugLogger : null);
	}

	private void StepAxisZones(JoystickState?[] states)
	{
		if (_AxisToButtonRoutes.IsEmpty)
		{
			_AxisZoneNextDeadlineTicks = null;
			return;
		}

		var now = _Time.GetTimestamp();
		long? earliestDeadline = null;

		foreach (var route in _AxisToButtonRoutes)
		{
			var inRange = false;
			if (states[route.SourceDeviceIndex] is { } state)
			{
				var value = route.SourceDevice.ReadNormalizedAxisValue(state, route.Source);
				inRange = value >= route.Min &&
				          (route.IncludeMax ? value <= route.Max : value < route.Max);
			}

			if (route.Mode == AxisZoneTriggerMode.Hold)
			{
				switch (inRange)
				{
					case true when !route.IsAsserting:
						route.Output.IncrementPressers();
						route.IsAsserting = true;
						break;
					case false when route.IsAsserting:
						route.Output.DecrementPressers();
						route.IsAsserting = false;
						break;
				}

				continue;
			}

			// Pulse mode.
			if (route.IsAsserting)
			{
				if (now >= route.PulseDeadlineTicks)
				{
					route.Output.DecrementPressers();
					route.IsAsserting = false;
					route.PulseCooldown = inRange;
				}
				else if (earliestDeadline is null || route.PulseDeadlineTicks < earliestDeadline)
				{
					earliestDeadline = route.PulseDeadlineTicks;
				}
			}
			else if (route.PulseCooldown)
			{
				if (!inRange)
				{
					route.PulseCooldown = false;
				}
			}
			else if (inRange)
			{
				route.Output.IncrementPressers();
				route.IsAsserting = true;
				route.PulseDeadlineTicks = now + route.PulseDurationTicks;
				if (earliestDeadline is null || route.PulseDeadlineTicks < earliestDeadline)
				{
					earliestDeadline = route.PulseDeadlineTicks;
				}
			}
		}

		_AxisZoneNextDeadlineTicks = earliestDeadline;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void ApplyButtons(JoystickState?[] states, DebugLogger? debugLines)
	{
		foreach (var route in _ButtonRoutes)
		{
			ButtonBinding? assertingBinding = null;
			foreach (var buttonBindingW in route.Bindings)
			{
				if (states[buttonBindingW.SourceDeviceIndex] is not { } state)
				{
					continue;
				}

				var buttonBinding = buttonBindingW.ButtonBinding;

				if (!state.IsButtonPressed(buttonBinding.ButtonNumber))
				{
					continue;
				}

				assertingBinding = buttonBinding;
				break;
			}

			var routeAsserting = assertingBinding is not null;
			if (routeAsserting != route.WasRouteAssertingLastFrame)
			{
				if (routeAsserting)
				{
					route.IncrementPressers();
				}
				else
				{
					route.DecrementPressers();
					// Falling edge ends the macro suppression window: a route's
					// next press cycle should be free to assert again.
					_Macros?.ClearSuppressionFor(route.TargetBinding);
				}

				route.SetWasRouteAssertingLastFrame(routeAsserting);
			}

			var isPressed = route is { Pressers: > 0, Suppressors: 0 };

			debugLines?.WriteLine(
				$"{(assertingBinding is { } bound
					? Utils.FormatInterpolation($"button {bound.DeviceId}:{bound.ButtonNumber} -> ")
					: isPressed
						? Utils.FormatInterpolation($"macro -> ")
						: NumberFormattingDebugInterpolatedStringHandler.Empty())}" +
				$"output{route.OutputDevice.DeviceId}:{route.TargetButton} = {(isPressed ? "down" : "up")}");

			route.OutputDevice.SetButtonState(route.TargetButton, isPressed);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void ApplyAxes(JoystickState?[] states, DebugLogger? debugLines)
	{
		foreach (var route in _AxisRoutes)
		{
			if (states[route.SourceDeviceIndex] is not { } state)
			{
				continue;
			}

			var device = route.SourceDevice;

			var sample = device.ReadAxisDebugSample(state, route.Source);
			var output = sample.NormalizedValue * route.Scale + route.Offset;
			if (route.RuntimeModifier is { } m)
			{
				output = m.Apply(output, states);
			}

			route.OutputDevice.SetAxisValue(route.OutputAxis, output);
			debugLines?.WriteLine(
				$"axis {route.Source.DeviceId}" +
				$"/{route.Source.Axis}" +
				$" -> " +
				$"output{route.OutputDevice.DeviceId}" +
				$"/{route.OutputAxis}" +
				$" raw={sample.RawValue}" +
				$" range={sample.RangeMin}" +
				$"..{sample.RangeMax}" +
				$" decoder={sample.DecoderKind}" +
				$" norm={sample.NormalizedValue}" +
				$" out={output}; {(
					route.RuntimeModifier is not IRuntimeAxisDebugView debugView
						? NumberFormattingDebugInterpolatedStringHandler.Empty()
						: debugView.GetDebugView())}");
		}
	}

	private void LogStartup(DebugLogger? debugLogger)
	{
		if (debugLogger is null)
		{
			return;
		}

		using var sortedDevices = DevicesById.Values.ToPooledList();
		sortedDevices.Sort(JoystickDevice.DeviceIdComparer);
		foreach (var device in sortedDevices)
		{
			debugLogger.WriteLine(
				$"device {device.DeviceId}: {device.Name} " +
				$"(instance '{device.InstanceName}', " +
				$"axes={device.Capabilities.NumAxes}, " +
				$"buttons={device.Capabilities.NumButtons}, " +
				$"povs={device.Capabilities.NumPovs})");
		}
	}

	public void Dispose()
	{
		_Macros?.Dispose();
		_LastReportedReadFailure.Dispose();
		DisposeDevices(DevicesById.Values);
		foreach (var outputDevice in _OutputDevices)
		{
			outputDevice.Dispose();
		}
	}

	internal static void DisposeDevices(IEnumerable<JoystickDevice> devices)
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}
}