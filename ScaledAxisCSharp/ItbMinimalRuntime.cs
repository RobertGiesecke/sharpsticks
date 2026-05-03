namespace ScaledAxisCSharp;

internal sealed class ItbMinimalRuntime
{
	private readonly ItbMinimalConfig _config;
	private readonly int _pollIntervalMs;
	private readonly JoystickDevice _leftDevice;
	private readonly JoystickDevice _rightDevice;
	private readonly VJoyDevice _vJoyDevice;
	private readonly AxisBinding _rightXAxis;
	private readonly AxisBinding _rightYAxis;
	private readonly AxisBinding _rightZAxis;
	private readonly AxisBinding _modifierAxis;
	private readonly AxisBinding _axis5OverrideAxis;
	private bool _secondaryFirePrevious;
	private int _pulse71RemainingMs;
	private int _pulse72RemainingMs;

	private ItbMinimalRuntime(
		ItbMinimalConfig config,
		JoystickDevice leftDevice,
		JoystickDevice rightDevice,
		VJoyDevice vJoyDevice,
		AxisBinding modifierAxis,
		AxisBinding rightXAxis,
		AxisBinding rightYAxis,
		AxisBinding rightZAxis,
		AxisBinding axis5OverrideAxis)
	{
		_config = config;
		_pollIntervalMs = config.PollIntervalMs;
		_leftDevice = leftDevice;
		_rightDevice = rightDevice;
		_vJoyDevice = vJoyDevice;
		_modifierAxis = modifierAxis;
		_rightXAxis = rightXAxis;
		_rightYAxis = rightYAxis;
		_rightZAxis = rightZAxis;
		_axis5OverrideAxis = axis5OverrideAxis;
	}

	public static ItbMinimalRuntime Build(ItbMinimalConfig config)
	{
		if (config.PollIntervalMs < 1)
		{
			throw new InvalidOperationException("PollIntervalMs must be at least 1.");
		}

		if (config.PulseMs < 0)
		{
			throw new InvalidOperationException("PulseMs must be zero or greater.");
		}

		var devices = JoystickDevice.EnumerateConnected();
		var leftDevice = ResolveDevice(devices, config.LeftDeviceName);
		var rightDevice = ResolveDevice(devices, config.RightDeviceName);

		var vJoyDevice = VJoyDevice.Open(
			config.VJoyDeviceId,
			[
				new ButtonRoute(leftDevice.DeviceId, 1, 40),
				new ButtonRoute(leftDevice.DeviceId, 11, 79),
				new ButtonRoute(rightDevice.DeviceId, 1, 1),
				new ButtonRoute(rightDevice.DeviceId, 18, 22),
			],
			[
				new AxisRoute(new AxisBinding(rightDevice.DeviceId, PhysicalAxis.X, AxisMode.Signed, false, 0.0), VJoyAxis.X, 1.0, 0.0),
				new AxisRoute(new AxisBinding(rightDevice.DeviceId, PhysicalAxis.Y, AxisMode.Signed, false, 0.0), VJoyAxis.Y, 1.0, 0.0),
				new AxisRoute(new AxisBinding(rightDevice.DeviceId, PhysicalAxis.Z, AxisMode.Signed, false, 0.0), VJoyAxis.Z, 1.0, 0.0),
			],
			[]);

		return new ItbMinimalRuntime(
			config,
			leftDevice,
			rightDevice,
			vJoyDevice,
			new AxisBinding(leftDevice.DeviceId, PhysicalAxisParser.Parse(config.ModifierAxis), AxisMode.Signed, false, 0.0),
			new AxisBinding(rightDevice.DeviceId, PhysicalAxis.X, AxisMode.Signed, false, 0.0),
			new AxisBinding(rightDevice.DeviceId, PhysicalAxis.Y, AxisMode.Signed, false, 0.0),
			new AxisBinding(rightDevice.DeviceId, PhysicalAxis.Z, AxisMode.Signed, false, 0.0),
			new AxisBinding(rightDevice.DeviceId, PhysicalAxisParser.Parse(config.Axis5OverrideAxis), AxisMode.Signed, false, 0.0));
	}

	public void Run(CancellationToken cancellationToken)
	{
		using (_vJoyDevice)
		{
			var leftReadFailed = false;
			var rightReadFailed = false;

			while (!cancellationToken.IsCancellationRequested)
			{
				if (!_leftDevice.TryRead(out var leftState, out var leftError))
				{
					if (!leftReadFailed && leftError is not null)
					{
						Console.Error.WriteLine(leftError);
						leftReadFailed = true;
					}

					if (cancellationToken.WaitHandle.WaitOne(_pollIntervalMs))
					{
						break;
					}

					continue;
				}

				leftReadFailed = false;

				if (!_rightDevice.TryRead(out var rightState, out var rightError))
				{
					if (!rightReadFailed && rightError is not null)
					{
						Console.Error.WriteLine(rightError);
						rightReadFailed = true;
					}

					if (cancellationToken.WaitHandle.WaitOne(_pollIntervalMs))
					{
						break;
					}

					continue;
				}

				rightReadFailed = false;

				ApplyAxes(leftState, rightState);
				ApplyButtons(leftState, rightState);
				AdvancePulses();

				if (cancellationToken.WaitHandle.WaitOne(_pollIntervalMs))
				{
					break;
				}
			}
		}
	}

	private void ApplyAxes(in JoystickState leftState, in JoystickState rightState)
	{
		var modifierValue = _leftDevice.ReadNormalizedAxis(leftState, _modifierAxis);
		var rightX = _rightDevice.ReadNormalizedAxis(rightState, _rightXAxis);
		var rightY = _rightDevice.ReadNormalizedAxis(rightState, _rightYAxis);
		var rightZ = _rightDevice.ReadNormalizedAxis(rightState, _rightZAxis);

		var precisionMode = leftState.IsButtonPressed(2) || rightState.IsButtonPressed(2) || rightState.IsButtonPressed(16);
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
			var axis5OverrideValue = _rightDevice.ReadNormalizedAxis(rightState, _axis5OverrideAxis);
			if (Math.Abs(axis5OverrideValue) >= _config.Axis5OverrideDeadzone)
			{
				outputX = axis5OverrideValue;
			}
		}

		_vJoyDevice.SetAxis(VJoyAxis.X, outputX);
		_vJoyDevice.SetAxis(VJoyAxis.Y, outputY);
		_vJoyDevice.SetAxis(VJoyAxis.Z, outputZ);
	}

	private void ApplyButtons(in JoystickState leftState, in JoystickState rightState)
	{
		_vJoyDevice.SetButton(1, rightState.IsButtonPressed(1));
		_vJoyDevice.SetButton(40, leftState.IsButtonPressed(1));
		_vJoyDevice.SetButton(79, leftState.IsButtonPressed(11));

		var secondaryFire = rightState.IsButtonPressed(18);
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

	private static JoystickDevice ResolveDevice(IReadOnlyList<JoystickDevice> devices, string productName)
	{
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
}
