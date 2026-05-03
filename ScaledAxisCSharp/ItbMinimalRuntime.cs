namespace ScaledAxisCSharp;

internal sealed class ItbMinimalRuntime
{
	private readonly ItbMinimalConfig _config;
	private readonly IReadOnlyDictionary<int, JoystickDevice> _devices;
	private readonly int _pollIntervalMs;
	private readonly VJoyDevice _vJoyDevice;
	private readonly AxisBinding _xAxis;
	private readonly AxisBinding _yAxis;
	private readonly AxisBinding _zAxis;
	private readonly AxisBinding _modifierAxis;
	private readonly AxisBinding _axis5OverrideAxis;
	private readonly ButtonBinding _primaryFireButton;
	private readonly ButtonBinding _leftPrimaryButton;
	private readonly ButtonBinding _leftAuxButton;
	private readonly ButtonBinding _secondaryFireButton;
	private readonly IReadOnlyList<ButtonBinding> _precisionButtons;
	private bool _secondaryFirePrevious;
	private int _pulse71RemainingMs;
	private int _pulse72RemainingMs;

	private ItbMinimalRuntime(
		ItbMinimalConfig config,
		IReadOnlyDictionary<int, JoystickDevice> devices,
		VJoyDevice vJoyDevice,
		AxisBinding xAxis,
		AxisBinding yAxis,
		AxisBinding zAxis,
		AxisBinding modifierAxis,
		AxisBinding axis5OverrideAxis,
		ButtonBinding primaryFireButton,
		ButtonBinding leftPrimaryButton,
		ButtonBinding leftAuxButton,
		ButtonBinding secondaryFireButton,
		IReadOnlyList<ButtonBinding> precisionButtons)
	{
		_config = config;
		_devices = devices;
		_pollIntervalMs = config.PollIntervalMs;
		_vJoyDevice = vJoyDevice;
		_xAxis = xAxis;
		_yAxis = yAxis;
		_zAxis = zAxis;
		_modifierAxis = modifierAxis;
		_axis5OverrideAxis = axis5OverrideAxis;
		_primaryFireButton = primaryFireButton;
		_leftPrimaryButton = leftPrimaryButton;
		_leftAuxButton = leftAuxButton;
		_secondaryFireButton = secondaryFireButton;
		_precisionButtons = precisionButtons;
	}

	public static ItbMinimalRuntime Build(ItbMinimalConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		config.Validate();

		if (config.PollIntervalMs < 1)
		{
			throw new InvalidOperationException("PollIntervalMs must be at least 1.");
		}

		if (config.PulseMs < 0)
		{
			throw new InvalidOperationException("PulseMs must be zero or greater.");
		}

		var connectedDevices = JoystickDevice.EnumerateConnected();
		var xAxis = ResolveAxisBinding(connectedDevices, config.XAxis);
		var yAxis = ResolveAxisBinding(connectedDevices, config.YAxis);
		var zAxis = ResolveAxisBinding(connectedDevices, config.ZAxis);
		var modifierAxis = ResolveAxisBinding(connectedDevices, config.ModifierAxis);
		var axis5OverrideAxis = ResolveAxisBinding(connectedDevices, config.Axis5OverrideAxis);
		var primaryFireButton = ResolveButtonBinding(connectedDevices, config.PrimaryFireButton);
		var leftPrimaryButton = ResolveButtonBinding(connectedDevices, config.LeftPrimaryButton);
		var leftAuxButton = ResolveButtonBinding(connectedDevices, config.LeftAuxButton);
		var secondaryFireButton = ResolveButtonBinding(connectedDevices, config.SecondaryFireButton);
		var precisionButtons = config.PrecisionButtons
			.Select(source => ResolveButtonBinding(connectedDevices, source))
			.ToArray();

		var selectedDevices = CollectDevices(
			connectedDevices,
			[
				xAxis.DeviceId,
				yAxis.DeviceId,
				zAxis.DeviceId,
				modifierAxis.DeviceId,
				axis5OverrideAxis.DeviceId,
				primaryFireButton.DeviceId,
				leftPrimaryButton.DeviceId,
				leftAuxButton.DeviceId,
				secondaryFireButton.DeviceId,
				.. precisionButtons.Select(binding => binding.DeviceId)
			]);

		var vJoyDevice = VJoyDevice.Open(
			config.VJoyDeviceId,
			[
				new ButtonRoute(primaryFireButton.DeviceId, primaryFireButton.ButtonNumber, 1),
				new ButtonRoute(leftPrimaryButton.DeviceId, leftPrimaryButton.ButtonNumber, 40),
				new ButtonRoute(leftAuxButton.DeviceId, leftAuxButton.ButtonNumber, 79),
				new ButtonRoute(secondaryFireButton.DeviceId, secondaryFireButton.ButtonNumber, 22),
			],
			[
				new AxisRoute(xAxis, VJoyAxis.X, 1.0, 0.0),
				new AxisRoute(yAxis, VJoyAxis.Y, 1.0, 0.0),
				new AxisRoute(zAxis, VJoyAxis.Z, 1.0, 0.0),
			],
			[]);

		return new ItbMinimalRuntime(
			config,
			selectedDevices,
			vJoyDevice,
			xAxis,
			yAxis,
			zAxis,
			modifierAxis,
			axis5OverrideAxis,
			primaryFireButton,
			leftPrimaryButton,
			leftAuxButton,
			secondaryFireButton,
			precisionButtons);
	}

	public void Run(CancellationToken cancellationToken)
	{
		using (_vJoyDevice)
		{
			var currentStates = new Dictionary<int, JoystickState>(_devices.Count);
			var lastReportedReadFailure = new HashSet<int>();

			while (!cancellationToken.IsCancellationRequested)
			{
				currentStates.Clear();

				foreach (var (deviceId, device) in _devices)
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

				ApplyAxes(currentStates);
				ApplyButtons(currentStates);
				AdvancePulses();

				if (cancellationToken.WaitHandle.WaitOne(_pollIntervalMs))
				{
					break;
				}
			}
		}
	}

	private void ApplyAxes(IReadOnlyDictionary<int, JoystickState> states)
	{
		if (!TryReadAxis(states, _modifierAxis, out var modifierValue) ||
		    !TryReadAxis(states, _xAxis, out var rightX) ||
		    !TryReadAxis(states, _yAxis, out var rightY) ||
		    !TryReadAxis(states, _zAxis, out var rightZ))
		{
			return;
		}

		var precisionMode = _precisionButtons.Any(binding => IsPressed(states, binding));
		var outputX = precisionMode
			? ApplyPrecisionCurve(rightX)
			: ApplyModifierCurve(rightX, modifierValue);
		var outputY = precisionMode
			? ApplyPrecisionCurve(rightY)
			: ApplyModifierCurve(rightY, modifierValue);
		var outputZ = precisionMode
			? ApplyPrecisionCurve(rightZ)
			: ApplyModifierCurve(rightZ, modifierValue);

		if (_config.EnableAxis5XOverride)
		{
			if (TryReadAxis(states, _axis5OverrideAxis, out var axis5OverrideValue) &&
			    Math.Abs(axis5OverrideValue) >= _config.Axis5OverrideDeadzone)
			{
				outputX = axis5OverrideValue;
			}
		}

		_vJoyDevice.SetAxis(VJoyAxis.X, outputX);
		_vJoyDevice.SetAxis(VJoyAxis.Y, outputY);
		_vJoyDevice.SetAxis(VJoyAxis.Z, outputZ);
	}

	private bool TryReadAxis(IReadOnlyDictionary<int, JoystickState> states, AxisBinding binding, out double value)
	{
		if (!states.TryGetValue(binding.DeviceId, out var state) ||
		    !_devices.TryGetValue(binding.DeviceId, out var device))
		{
			value = 0.0;
			return false;
		}

		value = device.ReadNormalizedAxis(state, binding);
		return true;
	}

	private bool IsPressed(IReadOnlyDictionary<int, JoystickState> states, ButtonBinding binding)
	{
		return states.TryGetValue(binding.DeviceId, out var state) && state.IsButtonPressed(binding.ButtonNumber);
	}

	private void ApplyButtons(IReadOnlyDictionary<int, JoystickState> states)
	{
		_vJoyDevice.SetButton(1, IsPressed(states, _primaryFireButton));
		_vJoyDevice.SetButton(40, IsPressed(states, _leftPrimaryButton));
		_vJoyDevice.SetButton(79, IsPressed(states, _leftAuxButton));

		var secondaryFire = IsPressed(states, _secondaryFireButton);
		_vJoyDevice.SetButton(22, secondaryFire);

		if (secondaryFire && !_secondaryFirePrevious)
		{
			_pulse72RemainingMs = _config.PulseMs;
		}

		if (!secondaryFire && _secondaryFirePrevious)
		{
			_pulse71RemainingMs = _config.PulseMs;
		}

		_secondaryFirePrevious = secondaryFire;

		_vJoyDevice.SetButton(71, _pulse71RemainingMs > 0);
		_vJoyDevice.SetButton(72, _pulse72RemainingMs > 0);
	}

	private void AdvancePulses()
	{
		if (_pulse71RemainingMs > 0)
		{
			_pulse71RemainingMs = Math.Max(0, _pulse71RemainingMs - _pollIntervalMs);
		}

		if (_pulse72RemainingMs > 0)
		{
			_pulse72RemainingMs = Math.Max(0, _pulse72RemainingMs - _pollIntervalMs);
		}
	}

	private double ApplyModifierCurve(double normalizedInput, double normalizedModifier)
	{
		var modifierSpan = _config.ModifierMax - _config.ModifierMin;
		if (modifierSpan == 0.0)
		{
			return 0.0;
		}

		var blend = Math.Clamp((normalizedModifier - _config.ModifierMin) / modifierSpan, 0.0, 1.0);
		var slope = _config.NormalSlope + ((_config.ModifierPrecisionSlope - _config.NormalSlope) * blend);
		return normalizedInput * slope;
	}

	private double ApplyPrecisionCurve(double normalizedInput)
	{
		return normalizedInput * _config.HoldPrecisionSlope;
	}

	private static Dictionary<int, JoystickDevice> CollectDevices(IReadOnlyList<JoystickDevice> devices, IEnumerable<int> deviceIds)
	{
		var byId = devices.ToDictionary(device => device.DeviceId);
		var selected = new Dictionary<int, JoystickDevice>();

		foreach (var deviceId in deviceIds.Distinct())
		{
			if (!byId.TryGetValue(deviceId, out var device))
			{
				throw new InvalidOperationException($"Configured joystick {deviceId} is not available via DirectInput.");
			}

			selected[deviceId] = device;
		}

		return selected;
	}

	private static AxisBinding ResolveAxisBinding(IReadOnlyList<JoystickDevice> devices, DeviceAxisSource source)
	{
		var device = ResolveDevice(devices, source.DeviceName);
		return new AxisBinding(device.DeviceId, PhysicalAxisParser.Parse(source.Axis), AxisMode.Signed, false, 0.0);
	}

	private static ButtonBinding ResolveButtonBinding(IReadOnlyList<JoystickDevice> devices, DeviceButtonSource source)
	{
		if (source.Button < 1)
		{
			throw new InvalidOperationException("ITB button sources are 1-based.");
		}

		var device = ResolveDevice(devices, source.DeviceName);
		return new ButtonBinding(device.DeviceId, source.Button);
	}

	private static JoystickDevice ResolveDevice(IReadOnlyList<JoystickDevice> devices, string productName)
	{
		if (string.IsNullOrWhiteSpace(productName))
		{
			throw new InvalidOperationException("DeviceName is required.");
		}

		var exactMatches = devices
			.Where(device => string.Equals(device.Name, productName, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (exactMatches.Length == 1)
		{
			return exactMatches[0];
		}

		if (exactMatches.Length > 1)
		{
			throw new InvalidOperationException(
				$"Multiple joystick devices match '{productName}'. Use a more specific device name.");
		}

		var partialMatches = devices
			.Where(device => device.Name.Contains(productName, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (partialMatches.Length == 1)
		{
			return partialMatches[0];
		}

		if (partialMatches.Length > 1)
		{
			throw new InvalidOperationException(
				$"Multiple joystick devices partially match '{productName}'. Use the full product name from the 'list' command.");
		}

		throw new InvalidOperationException($"No joystick device matched '{productName}'.");
	}

	private readonly record struct ButtonBinding(int DeviceId, int ButtonNumber);
}
