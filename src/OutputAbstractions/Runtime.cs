using System.Runtime.CompilerServices;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.OutputAbstractions;

//public sealed class Runtime : IOutputRuntimeContext, IDisposable
public sealed class Runtime<TInputDevice, TOutputDevice>
	: IOutputRuntimeContext<TInputDevice, TOutputDevice>, IButtonSinkContext
	where TInputDevice : JoystickDevice
	where TOutputDevice : OutputDevice
{
	public string Name { get; }
	private readonly DebugLogger? _DebugLogger;
	private readonly ImmutableArray<OutputAxisRoute> _AxisRoutes;
	private readonly OutputButtonState[] _OutputButtonStates;
	// Final-apply sink per output-button state index (vJoy button / key / mouse / scroll),
	// built once from each target so the apply loop never does a lookup.
	private readonly IButtonStateSink[] _Sinks;
	private readonly ImmutableArray<OutputButtonWithBindings> _ButtonRoutes;
	private readonly ImmutableArray<OutputAxisToButtonRoute> _AxisToButtonRoutes;
	private long? _AxisZoneNextDeadlineTicks;
	private readonly FrozenDictionary<OutputButtonBinding, OutputButtonStateIndex> _OutputButtonStateIndexByBinding;
	private readonly ImmutableArray<TInputDevice> _Devices;
	private readonly ImmutableArray<TOutputDevice> _OutputDevices;
	private readonly FrozenDictionary<uint, TOutputDevice> _OutputDevicesById;
	private readonly MacroEngine? _Macros;
	private readonly ITimeSource _Time;
	private readonly IInputSynthesizer? _InputSynthesizer;
	private readonly bool _InitializeInputSynthesizer;
	private readonly ImmutableArray<OutputMouseAxisRoute> _MouseAxisRoutes;
	private readonly ImmutableArray<OutputMouseButtonGroup> _MouseButtonRoutes;
	private readonly ImmutableArray<OutputScrollAxisRoute> _ScrollAxisRoutes;
	private readonly ImmutableArray<OutputScrollButtonRoute> _ScrollButtonRoutes;
	private long _MouseMoveLastTicks;
	private bool _MouseMoveHasLast;
	private long _ScrollLastTicks;
	private bool _ScrollHasLast;
	private long? _MouseButtonZoneNextDeadlineTicks;

	public ImmutableArray<TOutputDevice> OutputDevices => _OutputDevices;
	public FrozenDictionary<int, TInputDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<TInputDevice> Devices => _Devices;
	public ITimeSource TimeSource => _Time;
	public IInputSynthesizer? InputSynthesizer => _InputSynthesizer;

	public OutputButtonStateIndex? TryGetOutputStateIndex(OutputButtonBinding binding) =>
		_OutputButtonStateIndexByBinding.TryGetValue(binding, out var index) ? index : null;

	IInputSynthesizer? IButtonSinkContext.Synthesizer => _InputSynthesizer;

	IButtonStateSink IButtonSinkContext.CreateOutputButtonSink(OutputButtonBinding binding) =>
		new OutputButtonSink(_OutputDevicesById[binding.OutputDeviceId], binding.ButtonNumber);

	private record struct OutputButtonState
	{
		public int Pressers;
		public int Suppressors;
		public bool WasRouteAssertingLastFrame;

		public static void IncrementPressers(ref OutputButtonState outputButtonState) => outputButtonState.Pressers++;

		public static void IncrementSuppressors(ref OutputButtonState outputButtonState) =>
			outputButtonState.Suppressors++;

		public static void DecrementSuppressors(ref OutputButtonState outputButtonState) =>
			outputButtonState.Suppressors--;

		public static void DecrementPressers(ref OutputButtonState outputButtonState) => outputButtonState.Pressers--;
	}

	/// <summary>
	/// One entry per distinct output button (route- or macro-targeted, or both).
	/// Tracks current assertions across all sources via <see cref="Pressers"/>
	/// and macro release-overrides via <see cref="Suppressors"/>. The button is
	/// held when <c>Pressers &gt; 0 &amp;&amp; Suppressors == 0</c>.
	/// </summary>
	private sealed class OutputButtonWithBindings
	{
		public required OutputButtonStateIndex OutputButtonStateIndex { get; init; }
		public required TOutputDevice OutputDevice { get; init; }
		public required int TargetButton { get; init; }
		public required OutputButtonBinding TargetBinding { get; init; }

		public required bool IsFirstBinding { get; init; }
		public required bool IsLastBinding { get; init; }
		public required ButtonBinding? ButtonBinding { get; init; }
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

	private readonly record struct OutputMouseButtonSourceKey(int SourceDeviceIndex, int ButtonNumber);

	private sealed class OutputMouseButtonZoneSource
	{
		public required int SourceDeviceIndex { get; init; }
		public required TInputDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required double Min { get; init; }
		public required double Max { get; init; }
		public required bool IncludeMax { get; init; }
		public required AxisZoneTriggerMode Mode { get; init; }
		public required long PulseDurationTicks { get; init; }

		public ZoneActivation Zone;
	}

	private sealed class OutputMouseButtonGroup
	{
		public required OutputMouseButton Button { get; init; }
		public required ImmutableArray<OutputMouseButtonSourceKey> Sources { get; init; }
		public required ImmutableArray<OutputMouseButtonZoneSource> Zones { get; init; }
		public bool WasAsserted;
	}

	private sealed class OutputMouseAxisRoute
	{
		public required int SourceDeviceIndex { get; init; }
		public required TInputDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required MouseDirection Direction { get; init; }
		public required double Sensitivity { get; init; }
		public required IRuntimeAxisModifier? RuntimeModifier { get; init; }

		// Sub-pixel remainder carried across frames so slow movement isn't lost to
		// integer truncation.
		public double Accumulator;
	}

	private sealed class OutputScrollButtonRoute
	{
		public required int SourceDeviceIndex { get; init; }
		public required int ButtonNumber { get; init; }
		public required ScrollAxis Axis { get; init; }
		public required int Amount { get; init; }
		public required MouseScrollUnit Unit { get; init; }
		public bool WasPressed;
	}

	private sealed class OutputScrollAxisRoute
	{
		public required int SourceDeviceIndex { get; init; }
		public required TInputDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required ScrollAxis Axis { get; init; }
		public required MouseScrollUnit Unit { get; init; }
		public required double Sensitivity { get; init; }
		public required IRuntimeAxisModifier? RuntimeModifier { get; init; }

		// Sub-step remainder carried across frames so slow scrolling isn't lost to
		// integer truncation.
		public double Accumulator;
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
		public required OutputButtonStateIndex OutputButtonStateIndex { get; init; }

		public ZoneActivation Zone;
	}

	// Per-zone Hold/Pulse state, shared by vJoy-button and mouse-button zones.
	private struct ZoneActivation
	{
		public bool IsAsserting;
		public long PulseDeadlineTicks;
		public bool PulseCooldown;
	}

	/// <summary>
	/// Advances one zone's Hold/Pulse state for this frame and returns whether it is
	/// asserting. <c>Hold</c> asserts while in range; <c>Pulse</c> asserts for
	/// <paramref name="pulseDurationTicks"/> on entering range, then waits (cooldown)
	/// until the axis leaves the zone. The earliest live pulse deadline is folded into
	/// <paramref name="earliestDeadline"/> so the run loop can wake to end the pulse.
	/// </summary>
	private static bool AdvanceZoneActivation(
		bool inRange,
		AxisZoneTriggerMode mode,
		long pulseDurationTicks,
		long now,
		ref ZoneActivation zone,
		ref long? earliestDeadline)
	{
		if (mode == AxisZoneTriggerMode.Hold)
		{
			zone.IsAsserting = inRange;
			return inRange;
		}

		if (zone.IsAsserting)
		{
			if (now >= zone.PulseDeadlineTicks)
			{
				zone.IsAsserting = false;
				zone.PulseCooldown = inRange;
			}
			else if (earliestDeadline is null || zone.PulseDeadlineTicks < earliestDeadline)
			{
				earliestDeadline = zone.PulseDeadlineTicks;
			}
		}
		else if (zone.PulseCooldown)
		{
			if (!inRange)
			{
				zone.PulseCooldown = false;
			}
		}
		else if (inRange)
		{
			zone.IsAsserting = true;
			zone.PulseDeadlineTicks = now + pulseDurationTicks;
			if (earliestDeadline is null || zone.PulseDeadlineTicks < earliestDeadline)
			{
				earliestDeadline = zone.PulseDeadlineTicks;
			}
		}

		return zone.IsAsserting;
	}

	public Runtime(
		string name,
		DebugLogger? debugLogger,
		PooledDictionary<int, TInputDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		ImmutableArray<ButtonMacroRoute> macroRoutes,
		ImmutableArray<AxisToButtonRoute> axisToButtonRoutes,
		ImmutableArray<AxisToMouseRoute> axisToMouseRoutes,
		ImmutableArray<ButtonToMouseRoute> buttonToMouseRoutes,
		ImmutableArray<AxisToScrollRoute> axisToScrollRoutes,
		ImmutableArray<ButtonToScrollRoute> buttonToScrollRoutes,
		ImmutableArray<AxisToMouseButtonRoute> axisToMouseButtonRoutes,
		ImmutableArray<OutputButtonBinding> auxiliaryOutputButtons,
		ITimeSource timeSource,
		ImmutableArray<TOutputDevice> outputDevices,
		IInputSynthesizer? inputSynthesizer = null,
		bool initializeInputSynthesizer = true)
	{
		Name = name;
		_DebugLogger = debugLogger;
		_Time = timeSource;
		_InputSynthesizer = inputSynthesizer;
		_InitializeInputSynthesizer = initializeInputSynthesizer;
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
		var mergedAxisToMouseRoutes = axisToMouseRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedButtonToMouseRoutes = buttonToMouseRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedAxisToScrollRoutes = axisToScrollRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedButtonToScrollRoutes = buttonToScrollRoutes.MergeOrGetAll(mergeObjectContext);
		var mergedAxisToMouseButtonRoutes = axisToMouseButtonRoutes.MergeOrGetAll(mergeObjectContext);

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

		{
			using var flattenedButtonBindings = new PooledList<OutputButtonWithBindings>();
			using var outputButtonsStates = new PooledList<OutputButtonState>(allOutputButtons.Count);
			using var outputButtonStateIndexByBinding = new PooledDictionary<OutputButtonBinding, OutputButtonStateIndex>(allOutputButtons.Count);

			foreach (var binding in allOutputButtons)
			{
				outputButtonsStates.Add(new());
				var outputButtonStateIndex = new OutputButtonStateIndex(outputButtonsStates.Count - 1);
				outputButtonStateIndexByBinding.Add(binding, outputButtonStateIndex);
				var sources = bindingsByOutput.GetValueOrDefault(binding, []);
				if (sources is not { Length: > 0 })
				{
					flattenedButtonBindings.Add(new()
					{
						OutputButtonStateIndex = outputButtonStateIndex,
						// ReSharper disable once AccessToDisposedClosure
						OutputDevice = outputDevices[outputDeviceIndexes[binding.OutputDeviceId]],
						TargetButton = binding.ButtonNumber,
						TargetBinding = binding,
						ButtonBinding = null,
						SourceDeviceIndex = -1,
						IsFirstBinding = true,
						IsLastBinding = true,
					});
					continue;
				}
				var lastIndex = sources.Length - 1;
				for (var i = 0; i < sources.Length; i++)
				{
					var t = sources[i].Binding;
					flattenedButtonBindings.Add(new()
					{
						OutputButtonStateIndex = outputButtonStateIndex,
						// ReSharper disable once AccessToDisposedClosure
						OutputDevice = outputDevices[outputDeviceIndexes[binding.OutputDeviceId]],
						TargetButton = binding.ButtonNumber,
						TargetBinding = binding,
						ButtonBinding = t,
						SourceDeviceIndex = DeviceIndexesById[t.DeviceId],
						IsFirstBinding = i == 0,
						IsLastBinding = i == lastIndex,
					});
				}
			}

			_OutputButtonStates = [..outputButtonsStates.Span];
			_OutputButtonStateIndexByBinding = outputButtonStateIndexByBinding.ToFrozenDictionary();
			_ButtonRoutes =
			[
				..flattenedButtonBindings.Span,
			];
		}

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
		_OutputDevicesById = outputDevices.ToFrozenDictionary(device => device.DeviceId);

		// Build the apply sink for each output-button state index. Today every index is a
		// vJoy OutputButtonBinding; key/mouse/scroll targets join this map in later steps.
		_Sinks = new IButtonStateSink[_OutputButtonStates.Length];
		foreach (var (binding, index) in _OutputButtonStateIndexByBinding)
		{
			_Sinks[index.Value] = binding.CreateRuntimeSink(this);
		}
		
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
					OutputButtonStateIndex = _OutputButtonStateIndexByBinding[route.OutputBinding],
				};
			})
		];
		_MouseAxisRoutes =
		[
			..mergedAxisToMouseRoutes.Select(route =>
			{
				if (route.Movement.Kind != MovementKind.Relative)
				{
					throw new NotSupportedException(
						"Only relative mouse movement is implemented; absolute is not yet supported.");
				}

				return new OutputMouseAxisRoute
				{
					SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
					SourceDevice = DevicesById[route.Source.DeviceId],
					Source = route.Source,
					Direction = route.Direction,
					Sensitivity = route.Sensitivity,
					RuntimeModifier = route.Modifier?.CreateModifierRuntimeContext(this),
				};
			})
		];
		// A mouse button can be asserted by physical buttons and/or axis zones; group
		// both source kinds under the same button so StepMouseButtons ORs them.
		_MouseButtonRoutes =
		[
			..mergedButtonToMouseRoutes.Select(route => route.Button)
				.Concat(mergedAxisToMouseButtonRoutes.Select(route => route.Button))
				.Distinct()
				.Select(button => new OutputMouseButtonGroup
				{
					Button = button,
					Sources =
					[
						..mergedButtonToMouseRoutes
							.Where(route => route.Button == button)
							.Select(route => new OutputMouseButtonSourceKey(DeviceIndexesById[route.Source.DeviceId], route.Source.ButtonNumber)),
					],
					Zones =
					[
						..mergedAxisToMouseButtonRoutes
							.Where(route => route.Button == button)
							.Select(route => new OutputMouseButtonZoneSource
							{
								SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
								SourceDevice = DevicesById[route.Source.DeviceId],
								Source = route.Source,
								Min = route.Min,
								Max = route.Max,
								IncludeMax = route.IncludeMax,
								Mode = route.Mode,
								PulseDurationTicks = (long)(route.PulseDuration.TotalSeconds * timeSource.Frequency),
							}),
					],
				})
		];
		_ScrollAxisRoutes =
		[
			..mergedAxisToScrollRoutes.Select(route => new OutputScrollAxisRoute
			{
				SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
				SourceDevice = DevicesById[route.Source.DeviceId],
				Source = route.Source,
				Axis = route.Axis,
				Unit = route.Unit,
				Sensitivity = route.Sensitivity,
				RuntimeModifier = route.Modifier?.CreateModifierRuntimeContext(this),
			})
		];
		_ScrollButtonRoutes =
		[
			..mergedButtonToScrollRoutes.Select(route => new OutputScrollButtonRoute
			{
				SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
				ButtonNumber = route.Source.ButtonNumber,
				Axis = route.Axis,
				Amount = route.Amount,
				Unit = route.Unit,
			})
		];
		_CurrentStates = new JoystickState?[DevicesById.Count];
		_LastReportedReadFailure = new();
		_Macros = mergedMacroRoutes.IsEmpty
			? null
			: new MacroEngine(
				mergedMacroRoutes,
				DeviceIndexesById,
				this,
				IncrementPressers,
				DecrementPressers,
				IncrementSuppressors,
				DecrementSuppressors);
	}

	private ref OutputButtonState GetRefOutputButtonState(OutputButtonStateIndex b) => 
		ref _OutputButtonStates[b.Value];

	private void IncrementPressers(OutputButtonStateIndex b)
	{
		ref var x = ref GetRefOutputButtonState(b);
		OutputButtonState.IncrementPressers(ref x);
	}

	private void DecrementPressers(OutputButtonStateIndex b)
	{
		ref var x = ref GetRefOutputButtonState(b);
		OutputButtonState.DecrementPressers(ref x);
	}

	private void IncrementSuppressors(OutputButtonStateIndex b)
	{
		ref var x = ref GetRefOutputButtonState(b);
		OutputButtonState.IncrementSuppressors(ref x);
	}

	private void DecrementSuppressors(OutputButtonStateIndex b)
	{
		ref var x = ref GetRefOutputButtonState(b);
		OutputButtonState.DecrementSuppressors(ref x);
	}

	private readonly JoystickState?[] _CurrentStates;
	private readonly PooledSet<int> _LastReportedReadFailure;

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		debugLogger ??= _DebugLogger;
		try
		{
			// Prepare the synthesizer backend (e.g. create the uinput device) as the
			// runtime starts, unless the build opted out; first use covers it lazily.
			if (_InitializeInputSynthesizer)
			{
				_InputSynthesizer?.EnsureInitialized();
			}

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

		if (_MouseButtonZoneNextDeadlineTicks is { } mouseZoneDeadline &&
		    (deadline is null || mouseZoneDeadline < deadline))
		{
			deadline = mouseZoneDeadline;
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
		StepMouseAxes(currentStates);
		StepMouseButtons(currentStates);
		StepScrollAxes(currentStates);
		StepScrollButtons(currentStates);
		// Commit this frame's synthesized events (macro keys + mouse moves/buttons/scroll)
		// in one batch. No-op when no synthesizer is configured or none were emitted.
		_InputSynthesizer?.Flush();
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void StepMouseButtons(JoystickState?[] states)
	{
		var now = _Time.GetTimestamp();
		long? earliestDeadline = null;

		foreach (var group in _MouseButtonRoutes)
		{
			var asserted = false;
			foreach (var (sourceDeviceIndex, buttonNumber) in group.Sources)
			{
				if (states[sourceDeviceIndex] is { } state && state.IsButtonPressed(buttonNumber))
				{
					asserted = true;
					break;
				}
			}

			// Advance every zone (even once asserted) so pulse timers keep ticking, then
			// OR each zone's activation in — physical buttons and zones share the button.
			foreach (var zone in group.Zones)
			{
				var inRange = false;
				if (states[zone.SourceDeviceIndex] is { } state)
				{
					var value = zone.SourceDevice.ReadNormalizedAxisValue(state, zone.Source);
					inRange = value >= zone.Min && (zone.IncludeMax ? value <= zone.Max : value < zone.Max);
				}

				asserted |= AdvanceZoneActivation(
					inRange, zone.Mode, zone.PulseDurationTicks, now, ref zone.Zone, ref earliestDeadline);
			}

			if (asserted == group.WasAsserted)
			{
				continue;
			}

			if (asserted)
			{
				_InputSynthesizer?.MouseButtonDown(group.Button);
			}
			else
			{
				_InputSynthesizer?.MouseButtonUp(group.Button);
			}

			group.WasAsserted = asserted;
		}

		_MouseButtonZoneNextDeadlineTicks = earliestDeadline;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void StepMouseAxes(JoystickState?[] states)
	{
		if (_MouseAxisRoutes.IsEmpty)
		{
			return;
		}

		var now = _Time.GetTimestamp();
		var elapsedSeconds = _MouseMoveHasLast ? (now - _MouseMoveLastTicks) / (double)_Time.Frequency : 0.0;
		_MouseMoveLastTicks = now;
		_MouseMoveHasLast = true;

		var dx = 0;
		var dy = 0;
		foreach (var route in _MouseAxisRoutes)
		{
			if (states[route.SourceDeviceIndex] is not { } state)
			{
				continue;
			}

			var value = route.SourceDevice.ReadNormalizedAxisValue(state, route.Source);
			if (route.RuntimeModifier is { } m)
			{
				value = m.Apply(value, states);
			}

			route.Accumulator += value * route.Sensitivity * elapsedSeconds;
			var step = (int)route.Accumulator; // truncates toward zero; remainder carries
			route.Accumulator -= step;

			if (route.Direction == MouseDirection.X)
			{
				dx += step;
			}
			else
			{
				dy += step;
			}
		}

		if (dx != 0 || dy != 0)
		{
			_InputSynthesizer?.MoveMouseRelative(dx, dy);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void StepScrollButtons(JoystickState?[] states)
	{
		foreach (var route in _ScrollButtonRoutes)
		{
			var pressed = states[route.SourceDeviceIndex] is { } state && state.IsButtonPressed(route.ButtonNumber);

			if (pressed && !route.WasPressed)
			{
				var (vertical, horizontal) = route.Axis == ScrollAxis.Vertical
					? (route.Amount, 0)
					: (0, route.Amount);
				_InputSynthesizer?.Scroll(vertical, horizontal, route.Unit);
			}

			// TODO: auto-repeat while held — option to re-emit on an interval instead of
			// only on the rising edge. Edge-only one-shot for now.
			route.WasPressed = pressed;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void StepScrollAxes(JoystickState?[] states)
	{
		if (_ScrollAxisRoutes.IsEmpty)
		{
			return;
		}

		var now = _Time.GetTimestamp();
		var elapsedSeconds = _ScrollHasLast ? (now - _ScrollLastTicks) / (double)_Time.Frequency : 0.0;
		_ScrollLastTicks = now;
		_ScrollHasLast = true;

		var notchVertical = 0;
		var notchHorizontal = 0;
		var hiResVertical = 0;
		var hiResHorizontal = 0;

		foreach (var route in _ScrollAxisRoutes)
		{
			if (states[route.SourceDeviceIndex] is not { } state)
			{
				continue;
			}

			var value = route.SourceDevice.ReadNormalizedAxisValue(state, route.Source);
			if (route.RuntimeModifier is { } m)
			{
				value = m.Apply(value, states);
			}

			// Sensitivity is notches/sec at full deflection; high-res emits 1/120-notch steps.
			var unitScale = route.Unit == MouseScrollUnit.HighResolution ? HiResUnitsPerNotch : 1.0;
			route.Accumulator += value * route.Sensitivity * unitScale * elapsedSeconds;
			var step = (int)route.Accumulator; // truncates toward zero; remainder carries
			route.Accumulator -= step;
			if (step == 0)
			{
				continue;
			}

			if (route.Unit == MouseScrollUnit.HighResolution)
			{
				if (route.Axis == ScrollAxis.Vertical)
				{
					hiResVertical += step;
				}
				else
				{
					hiResHorizontal += step;
				}
			}
			else if (route.Axis == ScrollAxis.Vertical)
			{
				notchVertical += step;
			}
			else
			{
				notchHorizontal += step;
			}
		}

		if (notchVertical != 0 || notchHorizontal != 0)
		{
			_InputSynthesizer?.Scroll(notchVertical, notchHorizontal, MouseScrollUnit.Notch);
		}

		if (hiResVertical != 0 || hiResHorizontal != 0)
		{
			_InputSynthesizer?.Scroll(hiResVertical, hiResHorizontal, MouseScrollUnit.HighResolution);
		}
	}

	// One detent = 120 high-res units (kernel/Windows WHEEL_DELTA convention).
	private const double HiResUnitsPerNotch = 120.0;

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

			var wasAsserting = route.Zone.IsAsserting;
			var asserting = AdvanceZoneActivation(
				inRange, route.Mode, route.PulseDurationTicks, now, ref route.Zone, ref earliestDeadline);

			if (asserting == wasAsserting)
			{
				continue;
			}

			if (asserting)
			{
				OutputButtonState.IncrementPressers(ref _OutputButtonStates[route.OutputButtonStateIndex.Value]);
			}
			else
			{
				OutputButtonState.DecrementPressers(ref _OutputButtonStates[route.OutputButtonStateIndex.Value]);
			}
		}

		_AxisZoneNextDeadlineTicks = earliestDeadline;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void ApplyButtons(JoystickState?[] states, DebugLogger? debugLines)
	{
		// Bindings are laid out flat but grouped per output button, each group
		// running from IsFirstBinding to IsLastBinding. Within a group the
		// bindings OR together: the first pressed one wins, at which point we
		// fast-forward to the group's last binding — emulating the old inner-loop
		// `break` — and apply the state there. A group with no press falls
		// through to its last binding and applies "released". OutputButtonStateIndex,
		// OutputDevice and TargetButton are identical across a group, so applying
		// on the last binding is equivalent to applying on the one that asserted.
		var routes = _ButtonRoutes;
		for (var i = 0; i < routes.Length; i++)
		{
			ButtonBinding? assertingBinding = null;

			while (true)
			{
				var route = routes[i];
				if (route is { SourceDeviceIndex: >= 0, ButtonBinding: { } buttonBinding } &&
				    states[route.SourceDeviceIndex] is { } state &&
				    state.IsButtonPressed(buttonBinding.ButtonNumber))
				{
					assertingBinding = buttonBinding;
					while (!routes[i].IsLastBinding)
					{
						i++;
					}

					break;
				}

				if (route.IsLastBinding)
				{
					break;
				}

				i++;
			}

			UpdateRouteButtonState(assertingBinding, routes[i], debugLines);
		}
	}

	// Precondition: called only for a group's last binding (IsLastBinding).
	private void UpdateRouteButtonState(
		in ButtonBinding? assertingBinding,
		in OutputButtonWithBindings route,
		DebugLogger? debugLines)
	{
		var routeAsserting = assertingBinding is not null;
		ref var x = ref _OutputButtonStates[route.OutputButtonStateIndex.Value];

		if (routeAsserting != x.WasRouteAssertingLastFrame)
		{
			if (routeAsserting)
			{
				OutputButtonState.IncrementPressers(ref x);
			}
			else
			{
				OutputButtonState.DecrementPressers(ref x);
				// Falling edge ends the macro suppression window: a route's
				// next press cycle should be free to assert again.
				_Macros?.ClearSuppressionFor(route.OutputButtonStateIndex);
			}

			x.WasRouteAssertingLastFrame = routeAsserting;
		}

		var isPressed = x is { Pressers: > 0, Suppressors: 0 };

		debugLines?.WriteLine(
			$"{(assertingBinding is { } bound
				? Utils.FormatInterpolation($"button {bound.DeviceId}:{bound.ButtonNumber} -> ")
				: isPressed
					? Utils.FormatInterpolation($"macro -> ")
					: NumberFormattingDebugInterpolatedStringHandler.Empty())}" +
			$"output{route.OutputDevice.DeviceId}:{route.TargetButton} = {(isPressed ? "down" : "up")}");

		_Sinks[route.OutputButtonStateIndex.Value].SetButtonState(isPressed);
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