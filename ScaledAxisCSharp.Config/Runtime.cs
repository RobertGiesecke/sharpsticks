using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;
using Collections.Pooled;

namespace ScaledAxisCSharp.Config;

public sealed class Runtime : IDisposable
{
	public string Name { get; }
	private readonly ImmutableArray<VJoyAxisRoute> _AxisRoutes;
	private readonly ImmutableArray<VJoyButtonWithBindings> _ButtonRoutes;
	private readonly FrozenDictionary<int, JoystickDevice> _Devices;
	private readonly ImmutableArray<VJoyDevice> _VJoyDevices;

	private sealed class VJoyButtonWithBindings
	{
		public required int VJoyDeviceIndex { get; init; }
		public required int TargetButton { get; init; }
		public required PooledList<ButtonBinding> Bindings { get; init; }
	}

	private sealed class VJoyAxisRoute
	{
		public required int VJoyDeviceIndex { get; init; }
		public required AxisBinding Source { get; init; }
		public required PhysicalAxis VJoyAxis { get; init; }
		public required double Scale { get; init; }
		public required double Offset { get; init; }
		public required IAxisModifier? Modifier { get; init; }
	}

	public Runtime(
		string name,
		PooledDictionary<int, JoystickDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		ImmutableArray<VJoyDevice> vJoyDevices)
	{
		Name = name;
		_Devices = devices.ToFrozenDictionary();
		var vJoyDeviceIndexes = vJoyDevices
			.Select((device, index) => new { device.DeviceId, Index = index })
			.ToDictionary(t => (int)t.DeviceId, t => t.Index);
		_ButtonRoutes =
		[
			..buttonRoutes.GroupBy(t => (t.VJoyDeviceId, t.TargetButton))
				.Select(group => new VJoyButtonWithBindings
				{
					VJoyDeviceIndex = vJoyDeviceIndexes[group.Key.VJoyDeviceId],
					TargetButton = group.Key.TargetButton,
					Bindings = group.Select(t => t.Binding)
						.Distinct()
						.ToPooledList(),
				})
		];
		_AxisRoutes =
		[
			..axisRoutes.Select(route => new VJoyAxisRoute
			{
				VJoyDeviceIndex = vJoyDeviceIndexes[route.VJoyDeviceId],
				Source = route.Source,
				VJoyAxis = route.VJoyAxis,
				Scale = route.Scale,
				Offset = route.Offset,
				Modifier = route.Modifier,
			})
		];
		_VJoyDevices = vJoyDevices;
	}

	public readonly record struct BuildOptions
	{
		public required string Name { get; init; }
		public required ImmutableArray<JoystickDevice> ConnectedDevices { get; init; }
		public required ImmutableArray<ButtonRoute> ButtonRoutes { get; init; }
		public required ImmutableArray<AxisRoute> AxisRoutes { get; init; }
	}

	public static Runtime Build(BuildOptions options)
	{
		using var connectedDevicesById = options.ConnectedDevices
			.ToPooledDictionary(device => device.DeviceId);
		var referencedDeviceIds = new HashSet<int>();
		var buttonRoutes = options.ButtonRoutes;
		var axisRoutes = options.AxisRoutes;
		var claimedAxes = new HashSet<(int VJoyDeviceId, PhysicalAxis Axis)>();
		var referencedVJoyDeviceIds = new HashSet<int>();

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
			var source = AxisBinding.Parse(mapping.Source);
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
			Name = "ITB Minimal",
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
			using var currentStates = new PooledDictionary<int, JoystickState>(_Devices.Count);
			using var lastReportedReadFailure = new PooledSet<int>();
			LogStartup(debugLogger);

			var waitHandles = _Devices.Values
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

				currentStates.Clear();

				foreach (var (deviceId, device) in _Devices)
				{
					if (device.TryRead(out var state, out var error))
					{
						currentStates[deviceId] = state;
						lastReportedReadFailure.Remove(deviceId);
					}
					else if (lastReportedReadFailure.Add(deviceId) && error is not null)
					{
						Console.Error.WriteLine(error);
					}
				}

				var shouldLog = debugLogger?.ShouldLogNow() is true;
				var debugLines = shouldLog ? debugLinesScope.Instance : null;
				debugLines?.Clear();

				ApplyButtons(currentStates, debugLines);
				ApplyAxes(currentStates, debugLines);

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

	private void ApplyButtons(PooledDictionary<int, JoystickState> states, StringBuilder? debugLines)
	{
		foreach (var route in _ButtonRoutes)
		{
			bool? current = null;
			foreach (var buttonBinding in route.Bindings)
			{
				if (!states.TryGetValue(buttonBinding.DeviceId, out var state))
				{
					continue;
				}

				if (current is null)
				{
					current = state.IsButtonPressed(buttonBinding.ButtonNumber);
				}
				else
				{
					current = current.Value || state.IsButtonPressed(buttonBinding.ButtonNumber);
				}

				var isPressed = current is true;
				_VJoyDevices[route.VJoyDeviceIndex].SetButton(route.TargetButton, isPressed);

				if (debugLines is not null)
				{
					debugLines.Append("button ");
					debugLines.Append(buttonBinding.DeviceId);
					debugLines.Append(':');
					debugLines.Append(buttonBinding.ButtonNumber);
					debugLines.Append(" -> ");
					debugLines.Append("vjoy");
					debugLines.Append(_VJoyDevices[route.VJoyDeviceIndex].DeviceId);
					debugLines.Append(':');
					debugLines.Append(route.TargetButton);
					debugLines.Append(" = ");
					debugLines.AppendLine(isPressed ? "down" : "up");
				}
			}
		}
	}

	private void ApplyAxes(PooledDictionary<int, JoystickState> states, StringBuilder? debugLines)
	{
		foreach (var route in _AxisRoutes)
		{
			if (!states.TryGetValue(route.Source.DeviceId, out var state) ||
			    !_Devices.TryGetValue(route.Source.DeviceId, out var device))
			{
				continue;
			}

			var sample = device.ReadAxisDebugSample(state, route.Source);
			var output = sample.NormalizedValue * route.Scale + route.Offset;
			if (route.Modifier is { } m)
			{
				output = m.Apply(output, states, _Devices);
			}

			_VJoyDevices[route.VJoyDeviceIndex].SetAxis(route.VJoyAxis, output);

			if (debugLines is not null)
			{
				AppendAxisDebugLine(debugLines, route.Source.DeviceId, route.Source.Axis,
					_VJoyDevices[route.VJoyDeviceIndex].DeviceId, route.VJoyAxis, sample, output);
			}
		}
	}

	private void LogStartup(DebugLogger? debugLogger)
	{
		if (debugLogger is null)
		{
			return;
		}

		foreach (var device in _Devices.Values.OrderBy(device => device.DeviceId))
		{
			debugLogger.WriteLine(
				$"device {device.DeviceId}: {device.Name} (instance '{device.InstanceName}', axes={device.Caps.NumAxes}, buttons={device.Caps.NumButtons}, povs={device.Caps.NumPovs})");
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
		foreach (var route in _ButtonRoutes)
		{
			route.Bindings.Dispose();
		}

		DisposeDevices(_Devices.Values);
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