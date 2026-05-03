namespace ScaledAxisCSharp;

internal sealed class Runtime
{
	private readonly IReadOnlyList<AxisRoute> _AxisRoutes;
	private readonly IReadOnlyList<ButtonRoute> _ButtonRoutes;
	private readonly IReadOnlyDictionary<int, JoystickDevice> _Devices;
	private readonly int _PollIntervalMs;
	private readonly IReadOnlyList<ScaledAxisRoute> _ScaledAxisRoutes;
	private readonly VJoyDevice _VJoyDevice;

	private Runtime(
		int pollIntervalMs,
		IReadOnlyDictionary<int, JoystickDevice> devices,
		IReadOnlyList<ButtonRoute> buttonRoutes,
		IReadOnlyList<AxisRoute> axisRoutes,
		IReadOnlyList<ScaledAxisRoute> scaledAxisRoutes,
		VJoyDevice vJoyDevice)
	{
		_PollIntervalMs = pollIntervalMs;
		_Devices = devices;
		_ButtonRoutes = buttonRoutes;
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

		var connectedDevices = JoystickDevice.EnumerateConnected().ToDictionary(device => device.DeviceId);
		var referencedDeviceIds = new HashSet<int>();
		var buttonRoutes = new List<ButtonRoute>();
		var axisRoutes = new List<AxisRoute>();
		var scaledAxisRoutes = new List<ScaledAxisRoute>();
		var claimedAxes = new HashSet<VJoyAxis>();

		foreach (var mapping in config.ButtonMappings)
		{
			if (mapping.SourceButton < 1)
			{
				throw new InvalidOperationException("Source buttons are 1-based.");
			}

			if (mapping.TargetButton < 1)
			{
				throw new InvalidOperationException("Target buttons are 1-based.");
			}

			referencedDeviceIds.Add(mapping.SourceDeviceId);
			buttonRoutes.Add(new ButtonRoute(mapping.SourceDeviceId, mapping.SourceButton, mapping.TargetButton));
		}

		foreach (var mapping in config.AxisMappings)
		{
			var source = AxisBinding.Parse(mapping.Source);
			var targetAxis = VJoyAxisParser.Parse(mapping.TargetAxis);

			if (!claimedAxes.Add(targetAxis))
			{
				throw new InvalidOperationException($"Target axis '{mapping.TargetAxis}' is mapped more than once.");
			}

			referencedDeviceIds.Add(source.DeviceId);
			axisRoutes.Add(new AxisRoute(source, targetAxis, mapping.Scale, mapping.Offset));
		}

		foreach (var mapping in config.ScaledAxisMappings)
		{
			var valueSource = AxisBinding.Parse(mapping.ValueSource);
			var factorSource = AxisBinding.Parse(mapping.FactorSource);
			var targetAxis = VJoyAxisParser.Parse(mapping.TargetAxis);

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

		var devices = new Dictionary<int, JoystickDevice>();
		foreach (var deviceId in referencedDeviceIds)
		{
			if (!connectedDevices.TryGetValue(deviceId, out var device))
			{
				throw new InvalidOperationException($"Configured joystick {deviceId} is not available via WinMM.");
			}

			devices.Add(deviceId, device);
		}

		var vJoyDevice = VJoyDevice.Open(config.VJoyDeviceId, buttonRoutes, axisRoutes, scaledAxisRoutes);
		return new Runtime(config.PollIntervalMs, devices, buttonRoutes, axisRoutes, scaledAxisRoutes, vJoyDevice);
	}

	public void Run(CancellationToken cancellationToken)
	{
		using (_VJoyDevice)
		{
			var currentStates = new Dictionary<int, JoystickState>(_Devices.Count);
			var lastReportedReadFailure = new HashSet<int>();

			while (!cancellationToken.IsCancellationRequested)
			{
				currentStates.Clear();

				foreach (var (deviceId, device) in _Devices)
					if (device.TryRead(out var state, out var error))
					{
						currentStates[deviceId] = state;
						lastReportedReadFailure.Remove(deviceId);
					}
					else if (lastReportedReadFailure.Add(deviceId) && error is not null)
					{
						Console.Error.WriteLine(error);
					}

				ApplyButtons(currentStates);
				ApplyAxes(currentStates);

				if (cancellationToken.WaitHandle.WaitOne(_PollIntervalMs))
				{
					break;
				}
			}
		}
	}

	private void ApplyButtons(IReadOnlyDictionary<int, JoystickState> states)
	{
		var aggregatedTargets = new Dictionary<int, bool>();

		foreach (var route in _ButtonRoutes)
		{
			if (!states.TryGetValue(route.SourceDeviceId, out var state))
			{
				continue;
			}

			var pressed = state.IsButtonPressed(route.SourceButton);
			if (!aggregatedTargets.TryGetValue(route.TargetButton, out var current))
			{
				aggregatedTargets[route.TargetButton] = pressed;
			}
			else
			{
				aggregatedTargets[route.TargetButton] = current || pressed;
			}
		}

		foreach (var (button, isPressed) in aggregatedTargets) _VJoyDevice.SetButton(button, isPressed);
	}

	private void ApplyAxes(IReadOnlyDictionary<int, JoystickState> states)
	{
		foreach (var route in _AxisRoutes)
		{
			if (!states.TryGetValue(route.Source.DeviceId, out var state) ||
			    !_Devices.TryGetValue(route.Source.DeviceId, out var device))
			{
				continue;
			}

			var value = device.ReadNormalizedAxis(state, route.Source);
			value = value * route.Scale + route.Offset;
			_VJoyDevice.SetAxis(route.TargetAxis, value);
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

			var sourceValue = valueDevice.ReadNormalizedAxis(valueState, route.ValueSource);
			var factorValue = factorDevice.ReadNormalizedAxis(factorState, route.FactorSource);
			var factorT = route.FactorSource.Mode == AxisMode.Signed
				? Math.Clamp((factorValue + 1.0) * 0.5, 0.0, 1.0)
				: Math.Clamp(factorValue, 0.0, 1.0);

			var blendedFactor = route.FactorLow + (route.FactorHigh - route.FactorLow) * factorT;
			var output = sourceValue * blendedFactor * route.OutputScale + route.OutputOffset;
			_VJoyDevice.SetAxis(route.TargetAxis, output);
		}
	}
}