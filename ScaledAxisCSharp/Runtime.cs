using System.Text;
using Collections.Pooled;

namespace ScaledAxisCSharp;

internal sealed class Runtime: IDisposable
{
	private readonly IReadOnlyList<AxisRoute> _AxisRoutes;
	private readonly PooledList<VJoyButtonWithBindings> _ButtonRoutes = new();
	private readonly PooledDictionary<int, JoystickDevice> _Devices;
	private readonly int _PollIntervalMs;
	private readonly IReadOnlyList<ScaledAxisRoute> _ScaledAxisRoutes;
	private readonly VJoyDevice _VJoyDevice;

	private sealed class VJoyButtonWithBindings
	{
		public required int TargetButton { get; init; }
		public required PooledList<ButtonBinding> Bindings { get; init; }
	}

	public Runtime(
		int pollIntervalMs,
		PooledDictionary<int, JoystickDevice> devices,
		IReadOnlyList<ButtonRoute> buttonRoutes,
		IReadOnlyList<AxisRoute> axisRoutes,
		IReadOnlyList<ScaledAxisRoute> scaledAxisRoutes,
		VJoyDevice vJoyDevice)
	{
		_PollIntervalMs = pollIntervalMs;
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
		_ScaledAxisRoutes = scaledAxisRoutes;
		_VJoyDevice = vJoyDevice;
	}

	public static Runtime Build(AppConfig config)
	{
		if (config.PollIntervalMs < 1)
		{
			throw new InvalidOperationException("PollIntervalMs must be at least 1.");
		}


		using var connectedDevices = JoystickDevice.EnumerateConnected();
		using var connectedDevicesById = connectedDevices
			.ToPooledDictionary(device => device.DeviceId);
		var referencedDeviceIds = new HashSet<int>();
		var buttonRoutes = new List<ButtonRoute>();
		var axisRoutes = new List<AxisRoute>();
		var scaledAxisRoutes = new List<ScaledAxisRoute>();
		var claimedAxes = new HashSet<VJoyAxis>();

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

			referencedDeviceIds.Add(mapping.SourceBinding.DeviceId);
			buttonRoutes.Add(new ButtonRoute(mapping.SourceBinding, mapping.TargetButton));
		}

		foreach (var mapping in config.AxisMappings)
		{
			var source = AxisBinding.Parse(mapping.Source);
			var targetAxis = VJoyAxis.Parse(mapping.TargetAxis);

			if (!claimedAxes.Add(targetAxis))
			{
				throw new InvalidOperationException($"Target axis '{mapping.TargetAxis}' is mapped more than once.");
			}

			referencedDeviceIds.Add(source.DeviceId);
			axisRoutes.Add(new AxisRoute()
			{
				Source = source,
				TargetAxis = targetAxis,
				Scale = mapping.Scale,
				Offset = mapping.Offset,
				Modifier = mapping.AxisModifier
			});
		}

		foreach (var mapping in config.ScaledAxisMappings)
		{
			var valueSource = AxisBinding.Parse(mapping.ValueSource);
			var factorSource = AxisBinding.Parse(mapping.FactorSource);
			var targetAxis = VJoyAxis.Parse(mapping.TargetAxis);

			if (!claimedAxes.Add(targetAxis))
			{
				throw new InvalidOperationException($"Target axis '{mapping.TargetAxis}' is mapped more than once.");
			}

			referencedDeviceIds.Add(valueSource.DeviceId);
			referencedDeviceIds.Add(factorSource.DeviceId);
			scaledAxisRoutes.Add(new ScaledAxisRoute(
				valueSource,
				factorSource,
				targetAxis,
				mapping.FactorLow,
				mapping.FactorHigh,
				mapping.OutputScale,
				mapping.OutputOffset));
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

			var vJoyDevice = VJoyDevice.Open(config.VJoyDeviceId, buttonRoutes, axisRoutes, scaledAxisRoutes);
			try
			{
				return new Runtime(config.PollIntervalMs, devices, buttonRoutes, axisRoutes, scaledAxisRoutes,
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

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		using (_VJoyDevice)
		{
			using var currentStates = new PooledDictionary<int, JoystickState>(_Devices.Count);
			using var lastReportedReadFailure = new PooledSet<int>();
			LogStartup(debugLogger);

			StringBuilder? debugLines = null;
			while (!cancellationToken.IsCancellationRequested)
			{
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
				if (shouldLog)
				{
					debugLines ??= new StringBuilder();
				}

				ApplyButtons(currentStates, debugLines);
				ApplyAxes(currentStates, debugLines);

				if (shouldLog && debugLogger is not null && debugLines is not null)
				{
					debugLogger.WriteBlock(debugLines);
				}

				if (cancellationToken.WaitHandle.WaitOne(_PollIntervalMs))
				{
					break;
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
			_VJoyDevice.SetAxis(route.TargetAxis, output);

			if (debugLines is not null)
			{
				AppendAxisDebugLine(debugLines, route.Source.DeviceId, route.Source.Axis, route.TargetAxis, sample,
					output);
			}
		}

		foreach (var route in _ScaledAxisRoutes)
		{
			if (!states.TryGetValue(route.ValueSource.DeviceId, out var valueState) ||
			    !_Devices.TryGetValue(route.ValueSource.DeviceId, out var valueDevice))
			{
				continue;
			}

			if (!states.TryGetValue(route.FactorSource.DeviceId, out var factorState) ||
			    !_Devices.TryGetValue(route.FactorSource.DeviceId, out var factorDevice))
			{
				continue;
			}

			var valueSample = valueDevice.ReadAxisDebugSample(valueState, route.ValueSource);
			var factorSample = factorDevice.ReadAxisDebugSample(factorState, route.FactorSource);
			var sourceValue = valueSample.NormalizedValue;
			var factorValue = factorSample.NormalizedValue;
			var factorT = route.FactorSource.Mode == AxisMode.Signed
				? Math.Clamp((factorValue + 1.0) * 0.5, 0.0, 1.0)
				: Math.Clamp(factorValue, 0.0, 1.0);

			var blendedFactor = route.FactorLow + (route.FactorHigh - route.FactorLow) * factorT;
			var output = sourceValue * blendedFactor * route.OutputScale + route.OutputOffset;
			_VJoyDevice.SetAxis(route.TargetAxis, output);

			if (debugLines is not null)
			{
				debugLines.Append("scaled-axis ");
				debugLines.Append(route.TargetAxis);
				debugLines.Append(" value dev ");
				debugLines.Append(route.ValueSource.DeviceId);
				debugLines.Append('/');
				debugLines.Append(route.ValueSource.Axis);
				debugLines.Append(" raw=");
				debugLines.Append(valueSample.RawValue);
				debugLines.Append(" range=");
				debugLines.Append(valueSample.RangeMin);
				debugLines.Append("..");
				debugLines.Append(valueSample.RangeMax);
				debugLines.Append(" decoder=");
				debugLines.Append(valueSample.DecoderKind);
				debugLines.Append(" norm=");
				debugLines.Append(FormatDouble(valueSample.NormalizedValue));
				debugLines.Append(" factor dev ");
				debugLines.Append(route.FactorSource.DeviceId);
				debugLines.Append('/');
				debugLines.Append(route.FactorSource.Axis);
				debugLines.Append(" raw=");
				debugLines.Append(factorSample.RawValue);
				debugLines.Append(" range=");
				debugLines.Append(factorSample.RangeMin);
				debugLines.Append("..");
				debugLines.Append(factorSample.RangeMax);
				debugLines.Append(" decoder=");
				debugLines.Append(factorSample.DecoderKind);
				debugLines.Append(" norm=");
				debugLines.Append(FormatDouble(factorSample.NormalizedValue));
				debugLines.Append(" blend=");
				debugLines.Append(FormatDouble(blendedFactor));
				debugLines.Append(" out=");
				debugLines.AppendLine(FormatDouble(output));
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
		VJoyAxis targetAxis,
		AxisDebugSample sample,
		double output)
	{
		debugLines.Append("axis ");
		debugLines.Append(deviceId);
		debugLines.Append('/');
		debugLines.Append(axis);
		debugLines.Append(" -> ");
		debugLines.Append(targetAxis);
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