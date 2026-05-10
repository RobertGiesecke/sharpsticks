using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;
using Collections.Pooled;

namespace ScaledAxisCSharp.Config;

public sealed class Runtime : IRuntimeContext, IDisposable
{
	public string Name { get; }
	private readonly ImmutableArray<VJoyAxisRoute> _AxisRoutes;
	private readonly ImmutableArray<VJoyButtonWithBindings> _ButtonRoutes;
	private readonly ImmutableArray<JoystickDevice> _Devices;
	private readonly ImmutableArray<VJoyDevice> _VJoyDevices;

	public FrozenDictionary<int, JoystickDevice> DevicesById { get; }

	public FrozenDictionary<int, int> DeviceIndexesById { get; }

	public ImmutableArray<JoystickDevice> Devices => _Devices;

	public ImmutableArray<VJoyDevice> VJoyDevices => _VJoyDevices;

	private sealed class VJoyButtonWithBindings
	{
		public required VJoyDevice VJoyDevice { get; init; }
		public required int TargetButton { get; init; }
		public required ImmutableArray<ButtonBindingWithDeviceId> Bindings { get; init; }
	}

	private sealed class ButtonBindingWithDeviceId
	{
		public required ButtonBinding ButtonBinding { get; init; }
		public required int SourceDeviceIndex { get; init; }
	}

	private sealed class VJoyAxisRoute
	{
		public required VJoyDevice VJoyDevice { get; init; }
		public required int SourceDeviceIndex { get; init; }
		public required JoystickDevice SourceDevice { get; init; }
		public required AxisBinding Source { get; init; }
		public required PhysicalAxis VJoyAxis { get; init; }
		public required double Scale { get; init; }
		public required double Offset { get; init; }
		public required IAxisModifier? Modifier { get; init; }
		public required IRuntimeAxisModifier? RuntimeModifier { get; init; }
	}

	public Runtime(
		string name,
		PooledDictionary<int, JoystickDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		ImmutableArray<VJoyDevice> vJoyDevices)
	{
		Name = name;
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
		using var vJoyDeviceIndexes = vJoyDevices
			.Select((device, index) => new { device.DeviceId, Index = index })
			.ToPooledDictionary(t => t.DeviceId, t => t.Index);
		_ButtonRoutes =
		[
			..buttonRoutes.GroupBy(t => (t.VJoyDeviceId, t.TargetButton))
				.Select(group => new VJoyButtonWithBindings
				{
					// ReSharper disable once AccessToDisposedClosure
					VJoyDevice = vJoyDevices[vJoyDeviceIndexes[group.Key.VJoyDeviceId]],
					TargetButton = group.Key.TargetButton,
					Bindings =
					[
						..group.Select(t => t.Binding)
							.Distinct()
							.Select(t => new ButtonBindingWithDeviceId
							{
								ButtonBinding = t,
								SourceDeviceIndex = DeviceIndexesById[t.DeviceId],
							}),
					],
				})
		];
		_AxisRoutes =
		[
			..axisRoutes.Select(route => new VJoyAxisRoute
			{
				// ReSharper disable once AccessToDisposedClosure
				VJoyDevice = vJoyDevices[vJoyDeviceIndexes[route.VJoyDeviceId]],
				SourceDeviceIndex = DeviceIndexesById[route.Source.DeviceId],
				SourceDevice = DevicesById[route.Source.DeviceId],
				Source = route.Source,
				VJoyAxis = route.VJoyAxis,
				Scale = route.Scale,
				Offset = route.Offset,
				Modifier = route.Modifier,
				RuntimeModifier = route.Modifier?.CreateModifierRuntimeContext(this),
			})
		];
		_VJoyDevices = vJoyDevices;
	}

	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public required ImmutableArray<JoystickDevice> ConnectedDevices { get; init; }
		public ImmutableArray<ButtonRoute> ButtonRoutes { get; init; } = [];
		public ImmutableArray<AxisRoute> AxisRoutes { get; init; } = [];
	}

	public static Runtime Build(BuildOptions options)
	{
		using var connectedDevicesById = options.ConnectedDevices
			.ToPooledDictionary(device => device.DeviceId);
		var referencedDeviceIds = new HashSet<int>();
		var buttonRoutes = options.ButtonRoutes;
		var axisRoutes = options.AxisRoutes;
		var claimedAxes = new HashSet<(uint VJoyDeviceId, PhysicalAxis Axis)>();
		var referencedVJoyDeviceIds = new HashSet<uint>();

		foreach (var mapping in buttonRoutes)
		{
			if (mapping.Binding.ButtonNumber < 1)
			{
				throw new InvalidOperationException("Source buttons are 1-based.");
			}

			if (mapping.TargetButton < 1)
			{
				throw new InvalidOperationException("Target buttons are 1-based.");
			}

			if (mapping.VJoyDeviceId < 1)
			{
				throw new InvalidOperationException("vJoy device ids are 1-based.");
			}

			referencedDeviceIds.Add(mapping.Binding.DeviceId);
			referencedVJoyDeviceIds.Add(mapping.VJoyDeviceId);
		}

		foreach (var mapping in axisRoutes)
		{
			if (mapping.VJoyDeviceId < 1)
			{
				throw new InvalidOperationException("vJoy device ids are 1-based.");
			}

			if (!claimedAxes.Add((mapping.VJoyDeviceId, mapping.VJoyAxis)))
			{
				throw new InvalidOperationException(
					$"Target axis '{mapping.VJoyAxis}' on vJoy device {mapping.VJoyDeviceId} is mapped more than once.");
			}

			referencedDeviceIds.Add(mapping.Source.DeviceId);
			if (mapping.Modifier is { } m)
			{
				m.FillDevices(referencedDeviceIds);
			}

			referencedVJoyDeviceIds.Add(mapping.VJoyDeviceId);
		}


		var devices = new PooledDictionary<int, JoystickDevice>();
		try
		{
			foreach (var device in options.ConnectedDevices)
			{
				if (!referencedDeviceIds.Contains(device.DeviceId))
				{
					device.Dispose();
				}
			}

			foreach (var deviceId in referencedDeviceIds)
			{
				if (!connectedDevicesById.TryGetValue(deviceId, out var device))
				{
					throw new InvalidOperationException(
						$"Configured joystick {deviceId} is not available via DirectInput.");
				}

				devices.Add(deviceId, device);
			}

			var vJoyDevices = referencedVJoyDeviceIds
				.OrderBy(deviceId => deviceId)
				.Select(deviceId => VJoyDevice.Open(
					deviceId,
					buttonRoutes.Where(route => route.VJoyDeviceId == deviceId).ToArray(),
					axisRoutes.Where(route => route.VJoyDeviceId == deviceId).ToArray()))
				.ToImmutableArray();
			try
			{
				return new Runtime(
					options.Name,
					devices,
					buttonRoutes,
					axisRoutes,
					vJoyDevices);
			}
			catch
			{
				foreach (var vJoyDevice in vJoyDevices)
				{
					vJoyDevice.Dispose();
				}

				throw;
			}
		}
		catch
		{
			DisposeDevices(devices.Values);
			foreach (var device in options.ConnectedDevices)
			{
				if (!devices.TryGetValue(device.DeviceId, out var selected) || !ReferenceEquals(selected, device))
				{
					device.Dispose();
				}
			}

			devices.Dispose();
			throw;
		}
	}

	public static Runtime BuildFromConfig(AppConfig config)
	{
		var buildOptions = GetBuildOptionsFromConfig(config);
		return Build(buildOptions);
	}

	private static BuildOptions GetBuildOptionsFromConfig(AppConfig config)
	{
		using var connectedDevices = DirectInputJoystickDevice.EnumerateConnected();
		using var connectedDevicesById = connectedDevices
			.ToPooledDictionary(device => device.DeviceId);
		var buttonRoutes = new List<ButtonRoute>();
		var axisRoutes = new List<AxisRoute>();

		foreach (var mapping in config.ButtonMappings)
		{
			if (mapping.SourceBinding.ButtonNumber < 1)
			{
				throw new InvalidOperationException("Source buttons are 1-based.");
			}

			if (mapping.TargetButton < 1)
			{
				throw new InvalidOperationException("Target buttons are 1-based.");
			}

			buttonRoutes.Add(new ButtonRoute(mapping.SourceBinding, mapping.VJoyDeviceId ?? config.VJoyDeviceId,
				mapping.TargetButton));
		}

		foreach (var mapping in config.AxisMappings)
		{
			var source = InputAbstractions.AxisBinding.Parse(mapping.Source);
			var targetAxis = PhysicalAxis.Parse(mapping.TargetAxis);

			axisRoutes.Add(new AxisRoute
			{
				Source = source,
				VJoyDeviceId = mapping.VJoyDeviceId ?? config.VJoyDeviceId,
				VJoyAxis = targetAxis,
				Scale = mapping.Scale,
				Offset = mapping.Offset,
				Modifier = mapping.Modifier,
			});
		}

		var buildOptions = new BuildOptions
		{
			Name = config.Name ?? "unnamed",
			ConnectedDevices = [..connectedDevices],
			ButtonRoutes = [..buttonRoutes],
			AxisRoutes = [..axisRoutes],
		};
		return buildOptions;
	}

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		using var debugLinesScope = SharedPools.StringBuilder.GetInstance();
		try
		{
			var currentStates = new JoystickState?[DevicesById.Count];
			var currentStatesSpan = currentStates.AsSpan();

			ImmutableArray<int> deviceIds;
			{
				Span<int> deviceIdsSpan = stackalloc int[DevicesById.Count];
				var deviceIndex = -1;
				foreach (var keyValuePair in DevicesById)
				{
					deviceIndex += 1;
					deviceIdsSpan[deviceIndex] = keyValuePair.Value.DeviceId;
				}

				deviceIds = [..deviceIdsSpan];
			}

			using var lastReportedReadFailure = new PooledSet<int>();
			LogStartup(debugLogger);

			var waitHandles = DevicesById.Values
				.Select(d => d.DataAvailable)
				.Append(cancellationToken.WaitHandle)
				.ToArray();
			var cancelIndex = waitHandles.Length - 1;

			while (true)
			{
				if (WaitHandle.WaitAny(waitHandles) == cancelIndex)
				{
					break;
				}

				// clear all states
				currentStatesSpan.Fill(null);

				for (var deviceIndex = 0; deviceIndex < _Devices.Length; deviceIndex++)
				{
					var device = _Devices[deviceIndex];
					if (device.TryRead(out var state, out var error))
					{
						currentStates[deviceIndex] = state;
						lastReportedReadFailure.Remove(device.DeviceId);
					}
					else if (lastReportedReadFailure.Add(device.DeviceId) && error is not null)
					{
						Console.Error.WriteLine(error);
					}
				}

				var shouldLog = debugLogger?.ShouldLogNow() is true;
				var debugLines = shouldLog ? debugLinesScope.Instance : null;
				debugLines?.Clear();

				ApplyButtons(currentStates, debugLines);
				ApplyAxes( currentStates, debugLines);

				if (shouldLog && debugLogger is not null && debugLines is not null)
				{
					debugLogger.WriteBlock(debugLines);
				}
			}
		}
		finally
		{
			foreach (var vJoyDevice in _VJoyDevices)
			{
				vJoyDevice.Dispose();
			}
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
					debugLines.Append("vjoy");
					debugLines.Append(route.VJoyDevice.DeviceId);
					debugLines.Append(':');
					debugLines.Append(route.TargetButton);
					debugLines.Append(" = ");
					debugLines.AppendLine("down");
				}

				isPressed = true;
				break;
			}

			route.VJoyDevice.SetButton(route.TargetButton, isPressed);

			if (debugLines is not null && !isPressed)
			{
				debugLines.Append("button -> vjoy");
				debugLines.Append(route.VJoyDevice.DeviceId);
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

			route.VJoyDevice.SetAxis(route.VJoyAxis, output);

			if (debugLines is not null)
			{
				AppendAxisDebugLine(debugLines, route.Source.DeviceId, route.Source.Axis,
					route.VJoyDevice.DeviceId, route.VJoyAxis, sample, output);
			}
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
		PhysicalAxis axis,
		uint vJoyDeviceId,
		PhysicalAxis vJoyAxis,
		AxisDebugSample sample,
		double output)
	{
		debugLines.Append("axis ");
		debugLines.Append(deviceId);
		debugLines.Append('/');
		debugLines.Append(axis);
		debugLines.Append(" -> ");
		debugLines.Append("vjoy");
		debugLines.Append(vJoyDeviceId);
		debugLines.Append('/');
		debugLines.Append(vJoyAxis);
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
		DisposeDevices(DevicesById.Values);
		foreach (var vJoyDevice in _VJoyDevices)
		{
			vJoyDevice.Dispose();
		}
	}

	private static void DisposeDevices(IEnumerable<JoystickDevice> devices)
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}
}