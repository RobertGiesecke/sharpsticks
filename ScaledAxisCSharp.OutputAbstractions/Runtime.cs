using System.Text;

namespace ScaledAxisCSharp.OutputAbstractions;

public sealed class Runtime : IOutputRuntimeContext, IDisposable
{
	public string Name { get; }
	private readonly DebugLogger? _DebugLogger;
	private readonly ImmutableArray<OutputAxisRoute> _AxisRoutes;
	private readonly ImmutableArray<OutputButtonWithBindings> _ButtonRoutes;
	private readonly ImmutableArray<JoystickDevice> _Devices;
	private readonly ImmutableArray<OutputDevice> _OutputDevices;
	private readonly MacroEngine? _Macros;
	private readonly ITimeSource _Time;

	public ImmutableArray<OutputDevice> OutputDevices => _OutputDevices;
	public FrozenDictionary<int, JoystickDevice> DevicesById { get; }
	public FrozenDictionary<int, int> DeviceIndexesById { get; }
	public ImmutableArray<JoystickDevice> Devices => _Devices;

	private sealed class OutputButtonWithBindings
	{
		public required OutputDevice OutputDevice { get; init; }
		public required int TargetButton { get; init; }
		public required OutputButtonBinding TargetBinding { get; init; }
		public required ImmutableArray<ButtonBindingWithDeviceId> Bindings { get; init; }
	}

	private sealed class ButtonBindingWithDeviceId
	{
		public required ButtonBinding ButtonBinding { get; init; }
		public required int SourceDeviceIndex { get; init; }
	}

	private sealed class OutputAxisRoute
	{
		public required OutputDevice OutputDevice { get; init; }
		public required int SourceDeviceIndex { get; init; }
		public required JoystickDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required Axis OutputAxis { get; init; }
		public required double Scale { get; init; }
		public required double Offset { get; init; }
		public required IAxisModifier? Modifier { get; init; }
		public required IRuntimeAxisModifier? RuntimeModifier { get; init; }
	}

	public Runtime(
		string name,
		DebugLogger? debugLogger,
		PooledDictionary<int, JoystickDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		ImmutableArray<ButtonMacroRoute> macroRoutes,
		ImmutableArray<OutputButtonBinding> macroOutputButtons,
		ITimeSource timeSource,
		ImmutableArray<OutputDevice> outputDevices)
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

		// Macro-only output buttons get an OutputButtonWithBindings entry too
		// so ApplyButtons can OR the macro engine's held set in for them.
		using var allOutputButtons = new PooledSet<OutputButtonBinding>();
		foreach (var route in buttonRoutes)
		{
			allOutputButtons.Add(route.OutputBinding);
		}

		foreach (var macroBinding in macroOutputButtons)
		{
			allOutputButtons.Add(macroBinding);
		}

		using var bindingsByOutput = buttonRoutes
			.GroupBy(t => t.OutputBinding)
			.ToPooledDictionary(g => g.Key, g => g.ToArray());

		_ButtonRoutes =
		[
			..allOutputButtons.Select(binding =>
			{
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
			..axisRoutes.Select(route => new OutputAxisRoute
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
		_CurrentStates = new JoystickState?[DevicesById.Count];
		_LastReportedReadFailure = new();
		_Macros = macroRoutes.IsEmpty
			? null
			: new MacroEngine(macroRoutes, DeviceIndexesById, timeSource);
	}


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
		if (_Macros?.NextDeadlineTicks is not { } deadline)
		{
			return Timeout.Infinite;
		}

		var remaining = deadline - _Time.GetTimestamp();
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
		using var debugLinesScope = SharedPools.StringBuilder.GetInstance();

		var currentStates = _CurrentStates;
		currentStates.AsSpan().Fill(null);

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

		var shouldLog = debugLogger?.ShouldLogNow() is true;
		var debugLines = shouldLog ? debugLinesScope.Instance : null;
		debugLines?.Clear();

		_Macros?.Step(currentStates);
		ApplyButtons(currentStates, debugLines);
		ApplyAxes(currentStates, debugLines);

		if (shouldLog && debugLogger is not null && debugLines is not null)
		{
			debugLogger.WriteBlock(debugLines);
		}
	}

	private void ApplyButtons(JoystickState?[] states, StringBuilder? debugLines)
	{
		foreach (var route in _ButtonRoutes)
		{
			var isPressed = false;
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

				if (debugLines is not null)
				{
					debugLines.Append("button ");
					debugLines.Append(buttonBinding.DeviceId);
					debugLines.Append(':');
					debugLines.Append(buttonBinding.ButtonNumber);
					debugLines.Append(" -> ");
					debugLines.Append("output");
					debugLines.Append(route.OutputDevice.DeviceId);
					debugLines.Append(':');
					debugLines.Append(route.TargetButton);
					debugLines.Append(" = ");
					debugLines.AppendLine("down");
				}

				isPressed = true;
				break;
			}

			if (!isPressed && _Macros is not null && _Macros.IsHeld(route.TargetBinding))
			{
				if (debugLines is not null)
				{
					debugLines.Append("macro -> output");
					debugLines.Append(route.OutputDevice.DeviceId);
					debugLines.Append(':');
					debugLines.Append(route.TargetButton);
					debugLines.AppendLine(" = down");
				}

				isPressed = true;
			}

			route.OutputDevice.SetButtonState(route.TargetButton, isPressed);

			if (debugLines is not null && !isPressed)
			{
				debugLines.Append("button -> output");
				debugLines.Append(route.OutputDevice.DeviceId);
				debugLines.Append(':');
				debugLines.Append(route.TargetButton);
				debugLines.AppendLine(" = up");
			}
		}
	}

	private void ApplyAxes(JoystickState?[] states, StringBuilder? debugLines)
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

			if (debugLines is null)
			{
				continue;
			}

			AppendAxisDebugLine(
				debugLines,
				route.Source.DeviceId,
				route.Source.Axis,
				route.OutputDevice.DeviceId,
				route.OutputAxis,
				sample,
				output);

			if (route.RuntimeModifier is not IRuntimeAxisDebugView debugView ||
			    debugView.GetDebugView() is not { Length: > 0 } debugText)
			{
				continue;
			}

			debugLines.Append("  ");
			debugLines.AppendLine(debugText);
		}
	}

	private void LogStartup(DebugLogger? debugLogger)
	{
		if (debugLogger is null)
		{
			return;
		}

		foreach (var device in DevicesById.Values.OrderBy(device => device.DeviceId))
		{
			debugLogger.WriteLine(
				$"device {device.DeviceId}: {device.Name} (instance '{device.InstanceName}', axes={device.Capabilities.NumAxes}, buttons={device.Capabilities.NumButtons}, povs={device.Capabilities.NumPovs})");
		}
	}

	private static void AppendAxisDebugLine(
		StringBuilder debugLines,
		int deviceId,
		Axis axis,
		uint outputDeviceId,
		Axis outputAxis,
		AxisDebugSample sample,
		double output)
	{
		debugLines.Append("axis ");
		debugLines.Append(deviceId);
		debugLines.Append('/');
		debugLines.Append(axis);
		debugLines.Append(" -> ");
		debugLines.Append("output");
		debugLines.Append(outputDeviceId);
		debugLines.Append('/');
		debugLines.Append(outputAxis);
		debugLines.Append(" raw=");
		debugLines.Append(sample.RawValue);
		debugLines.Append(" range=");
		debugLines.Append(sample.RangeMin);
		debugLines.Append("..");
		debugLines.Append(sample.RangeMax);
		debugLines.Append(" decoder=");
		debugLines.Append(sample.DecoderKind);
		debugLines.Append(" norm=");
		debugLines.Append(FormatDouble(sample.NormalizedValue));
		debugLines.Append(" out=");
		debugLines.AppendLine(FormatDouble(output));
	}

	private static string FormatDouble(double value)
	{
		return value.ToString("0.0000");
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