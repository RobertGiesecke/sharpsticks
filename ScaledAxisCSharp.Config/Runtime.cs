using System.Collections.Immutable;
using System.Text;
using Collections.Pooled;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Config;

public sealed class Runtime : IDisposable
{
	public string Name { get; }
	private readonly ImmutableArray<AxisRoute> _AxisRoutes;
	private readonly PooledList<VJoyButtonWithBindings> _ButtonRoutes;
	private readonly PooledDictionary<int, JoystickDevice> _Devices;
	private readonly VJoyDevice _VJoyDevice;

	private sealed class VJoyButtonWithBindings
	{
		public required int TargetButton { get; init; }
		public required PooledList<ButtonBinding> Bindings { get; init; }
	}

	public Runtime(
		string name,
		PooledDictionary<int, JoystickDevice> devices,
		ImmutableArray<ButtonRoute> buttonRoutes,
		ImmutableArray<AxisRoute> axisRoutes,
		VJoyDevice vJoyDevice)
	{
		Name = name;
		_Devices = devices;
		_ButtonRoutes = buttonRoutes.GroupBy(t => t.TargetButton)
			.Select(group => new VJoyButtonWithBindings
			{
				TargetButton = group.Key,
				Bindings = group.Select(t => t.Binding)
					.Distinct()
					.ToPooledList(),
			})
			.ToPooledList();
		_AxisRoutes = axisRoutes;
		_VJoyDevice = vJoyDevice;
	}

	public readonly record struct BuildOptions
	{
		public required string Name { get; init; }
		public required int VJoyDeviceId { get; init; }
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
		var claimedAxes = new HashSet<PhysicalAxis>();

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

			referencedDeviceIds.Add(mapping.Binding.DeviceId);
		}

		foreach (var mapping in axisRoutes)
		{
			if (!claimedAxes.Add(mapping.VJoyAxis))
			{
				throw new InvalidOperationException($"Target axis '{mapping.VJoyAxis}' is mapped more than once.");
			}

			referencedDeviceIds.Add(mapping.Source.DeviceId);
		}


		var devices = new PooledDictionary<int, JoystickDevice>();
		try
		{
			foreach (var deviceId in referencedDeviceIds)
			{
				if (!connectedDevicesById.TryGetValue(deviceId, out var device))
				{
					throw new InvalidOperationException(
						$"Configured joystick {deviceId} is not available via DirectInput.");
				}

				devices.Add(deviceId, device);
			}

			var vJoyDevice = VJoyDevice.Open(options.VJoyDeviceId, buttonRoutes, axisRoutes);
			try
			{
				return new Runtime(
					options.Name,
					devices,
					buttonRoutes,
					axisRoutes,
					vJoyDevice);
			}
			catch
			{
				vJoyDevice.Dispose();
				throw;
			}
		}
		catch
		{
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

			buttonRoutes.Add(new ButtonRoute(mapping.SourceBinding, mapping.TargetButton));
		}

		foreach (var mapping in config.AxisMappings)
		{
			var source = AxisBinding.Parse(mapping.Source);
			var targetAxis = PhysicalAxis.Parse(mapping.TargetAxis);

			axisRoutes.Add(new AxisRoute
			{
				Source = source,
				VJoyAxis = targetAxis,
				Scale = mapping.Scale,
				Offset = mapping.Offset,
				Modifier = mapping.Modifier,
			});
		}

		var buildOptions = new BuildOptions
		{
			Name = "ITB Minimal",
			VJoyDeviceId = config.VJoyDeviceId,
			ConnectedDevices = [..connectedDevices],
			ButtonRoutes = [..buttonRoutes],
			AxisRoutes = [..axisRoutes],
		};
		return buildOptions;
	}

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		using var debugLinesScope = SharedPools.StringBuilder.GetInstance();
		using (_VJoyDevice)
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
				_VJoyDevice.SetButton(route.TargetButton, isPressed);

				if (debugLines is not null)
				{
					debugLines.Append("button ");
					debugLines.Append(buttonBinding.DeviceId);
					debugLines.Append(':');
					debugLines.Append(buttonBinding.ButtonNumber);
					debugLines.Append(" -> ");
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

			_VJoyDevice.SetAxis(route.VJoyAxis, output);

			if (debugLines is not null)
			{
				AppendAxisDebugLine(debugLines, route.Source.DeviceId, route.Source.Axis, route.VJoyAxis, sample,
					output);
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
		PhysicalAxis vJoyAxis,
		AxisDebugSample sample,
		double output)
	{
		debugLines.Append("axis ");
		debugLines.Append(deviceId);
		debugLines.Append('/');
		debugLines.Append(axis);
		debugLines.Append(" -> ");
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
		_ButtonRoutes.Dispose();
		_Devices.Dispose();
		_VJoyDevice.Dispose();
	}
}