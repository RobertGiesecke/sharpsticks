using System.Text;

namespace ScaledAxisCSharp;

internal sealed class ItbMinimalRuntime
{
	private readonly ItbMinimalConfig _Config;
	private readonly IReadOnlyDictionary<int, JoystickDevice> _Devices;
	private readonly int _PollIntervalMs;
	private readonly VJoyDevice _VJoyDevice;
	private readonly AxisBinding _XAxis;
	private readonly AxisBinding _YAxis;
	private readonly AxisBinding _ZAxis;
	private readonly AxisBinding _ModifierAxis;
	private readonly AxisBinding _Axis5OverrideAxis;
	private readonly ButtonBinding _PrimaryFireButton;
	private readonly ButtonBinding _LeftPrimaryButton;
	private readonly ButtonBinding _LeftAuxButton;
	private readonly ButtonBinding _SecondaryFireButton;
	private readonly IReadOnlyList<ButtonBinding> _PrecisionButtons;
	private bool _SecondaryFirePrevious;
	private int _Pulse71RemainingMs;
	private int _Pulse72RemainingMs;

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
		_Config = config;
		_Devices = devices;
		_PollIntervalMs = config.PollIntervalMs;
		_VJoyDevice = vJoyDevice;
		_XAxis = xAxis;
		_YAxis = yAxis;
		_ZAxis = zAxis;
		_ModifierAxis = modifierAxis;
		_Axis5OverrideAxis = axis5OverrideAxis;
		_PrimaryFireButton = primaryFireButton;
		_LeftPrimaryButton = leftPrimaryButton;
		_LeftAuxButton = leftAuxButton;
		_SecondaryFireButton = secondaryFireButton;
		_PrecisionButtons = precisionButtons;
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

	public void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null)
	{
		using (_VJoyDevice)
		{
			var currentStates = new Dictionary<int, JoystickState>(_Devices.Count);
			var lastReportedReadFailure = new HashSet<int>();
			LogStartup(debugLogger);

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

				var debugLines = debugLogger?.ShouldLogNow() == true ? new StringBuilder() : null;
				ApplyAxes(currentStates, debugLines);
				ApplyButtons(currentStates, debugLines);
				AdvancePulses();

				if (debugLogger is not null && debugLines is not null)
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

	private void ApplyAxes(IReadOnlyDictionary<int, JoystickState> states, StringBuilder? debugLines)
	{
		if (!TryReadAxisSample(states, _ModifierAxis, out var modifierSample) ||
		    !TryReadAxisSample(states, _XAxis, out var xSample) ||
		    !TryReadAxisSample(states, _YAxis, out var ySample) ||
		    !TryReadAxisSample(states, _ZAxis, out var zSample))
		{
			return;
		}

		var modifierValue = modifierSample.NormalizedValue;
		var rightX = xSample.NormalizedValue;
		var rightY = ySample.NormalizedValue;
		var rightZ = zSample.NormalizedValue;
		var precisionMode = _PrecisionButtons.Any(binding => IsPressed(states, binding));
		var outputX = precisionMode
			? ApplyPrecisionCurve(rightX)
			: ApplyModifierCurve(rightX, modifierValue);
		var outputY = precisionMode
			? ApplyPrecisionCurve(rightY)
			: ApplyModifierCurve(rightY, modifierValue);
		var outputZ = precisionMode
			? ApplyPrecisionCurve(rightZ)
			: ApplyModifierCurve(rightZ, modifierValue);

		AxisDebugSample? axis5OverrideSample = null;
		var axis5OverrideActive = false;
		if (_Config.EnableAxis5XOverride && TryReadAxisSample(states, _Axis5OverrideAxis, out var overrideSample))
		{
			axis5OverrideSample = overrideSample;
			if (Math.Abs(overrideSample.NormalizedValue) >= _Config.Axis5OverrideDeadzone)
			{
				axis5OverrideActive = true;
				outputX = overrideSample.NormalizedValue;
			}
		}

		_VJoyDevice.SetAxis(VJoyAxis.X, outputX);
		_VJoyDevice.SetAxis(VJoyAxis.Y, outputY);
		_VJoyDevice.SetAxis(VJoyAxis.Z, outputZ);

		if (debugLines is not null)
		{
			debugLines.Append("modifier raw=");
			debugLines.Append(modifierSample.RawValue);
			debugLines.Append(" range=");
			debugLines.Append(modifierSample.RangeMin);
			debugLines.Append("..");
			debugLines.Append(modifierSample.RangeMax);
			debugLines.Append(" norm=");
			debugLines.Append(FormatDouble(modifierValue));
			debugLines.Append(" precision=");
			debugLines.AppendLine(precisionMode ? "on" : "off");

			AppendAxisDebugLine(debugLines, "x", xSample, outputX);
			AppendAxisDebugLine(debugLines, "y", ySample, outputY);
			AppendAxisDebugLine(debugLines, "z", zSample, outputZ);

			if (axis5OverrideSample is { } axis5Sample)
			{
				debugLines.Append("axis5-override raw=");
				debugLines.Append(axis5Sample.RawValue);
				debugLines.Append(" range=");
				debugLines.Append(axis5Sample.RangeMin);
				debugLines.Append("..");
				debugLines.Append(axis5Sample.RangeMax);
				debugLines.Append(" norm=");
				debugLines.Append(FormatDouble(axis5Sample.NormalizedValue));
				debugLines.Append(" active=");
				debugLines.AppendLine(axis5OverrideActive ? "yes" : "no");
			}
		}
	}

	private bool TryReadAxis(IReadOnlyDictionary<int, JoystickState> states, AxisBinding binding, out double value)
	{
		if (TryReadAxisSample(states, binding, out var sample))
		{
			value = sample.NormalizedValue;
			return true;
		}

		value = 0.0;
		return false;
	}

	private bool TryReadAxisSample(IReadOnlyDictionary<int, JoystickState> states, AxisBinding binding, out AxisDebugSample sample)
	{
		if (!states.TryGetValue(binding.DeviceId, out var state) ||
		    !_Devices.TryGetValue(binding.DeviceId, out var device))
		{
			sample = default;
			return false;
		}

		sample = device.ReadAxisDebugSample(state, binding);
		return true;
	}

	private bool IsPressed(IReadOnlyDictionary<int, JoystickState> states, ButtonBinding binding)
	{
		return states.TryGetValue(binding.DeviceId, out var state) && state.IsButtonPressed(binding.ButtonNumber);
	}

	private void ApplyButtons(IReadOnlyDictionary<int, JoystickState> states, StringBuilder? debugLines)
	{
		var primaryFire = IsPressed(states, _PrimaryFireButton);
		var leftPrimary = IsPressed(states, _LeftPrimaryButton);
		var leftAux = IsPressed(states, _LeftAuxButton);
		var secondaryFire = IsPressed(states, _SecondaryFireButton);

		_VJoyDevice.SetButton(1, primaryFire);
		_VJoyDevice.SetButton(40, leftPrimary);
		_VJoyDevice.SetButton(79, leftAux);
		_VJoyDevice.SetButton(22, secondaryFire);

		if (secondaryFire && !_SecondaryFirePrevious)
		{
			_Pulse72RemainingMs = _Config.PulseMs;
		}

		if (!secondaryFire && _SecondaryFirePrevious)
		{
			_Pulse71RemainingMs = _Config.PulseMs;
		}

		_SecondaryFirePrevious = secondaryFire;

		_VJoyDevice.SetButton(71, _Pulse71RemainingMs > 0);
		_VJoyDevice.SetButton(72, _Pulse72RemainingMs > 0);

		if (debugLines is not null)
		{
			debugLines.Append("buttons primary=");
			debugLines.Append(primaryFire ? "down" : "up");
			debugLines.Append(" left-primary=");
			debugLines.Append(leftPrimary ? "down" : "up");
			debugLines.Append(" left-aux=");
			debugLines.Append(leftAux ? "down" : "up");
			debugLines.Append(" secondary=");
			debugLines.Append(secondaryFire ? "down" : "up");
			debugLines.Append(" pulse71=");
			debugLines.Append(_Pulse71RemainingMs > 0 ? "on" : "off");
			debugLines.Append(" pulse72=");
			debugLines.AppendLine(_Pulse72RemainingMs > 0 ? "on" : "off");
		}
	}

	private void AdvancePulses()
	{
		if (_Pulse71RemainingMs > 0)
		{
			_Pulse71RemainingMs = Math.Max(0, _Pulse71RemainingMs - _PollIntervalMs);
		}

		if (_Pulse72RemainingMs > 0)
		{
			_Pulse72RemainingMs = Math.Max(0, _Pulse72RemainingMs - _PollIntervalMs);
		}
	}

	private double ApplyModifierCurve(double normalizedInput, double normalizedModifier)
	{
		var modifierSpan = _Config.ModifierMax - _Config.ModifierMin;
		if (modifierSpan == 0.0)
		{
			return 0.0;
		}

		var blend = Math.Clamp((normalizedModifier - _Config.ModifierMin) / modifierSpan, 0.0, 1.0);
		var slope = _Config.NormalSlope + ((_Config.ModifierPrecisionSlope - _Config.NormalSlope) * blend);
		return normalizedInput * slope;
	}

	private double ApplyPrecisionCurve(double normalizedInput)
	{
		return normalizedInput * _Config.HoldPrecisionSlope;
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

	private static void AppendAxisDebugLine(StringBuilder debugLines, string label, AxisDebugSample sample, double output)
	{
		debugLines.Append(label);
		debugLines.Append(" raw=");
		debugLines.Append(sample.RawValue);
		debugLines.Append(" range=");
		debugLines.Append(sample.RangeMin);
		debugLines.Append("..");
		debugLines.Append(sample.RangeMax);
		debugLines.Append(" norm=");
		debugLines.Append(FormatDouble(sample.NormalizedValue));
		debugLines.Append(" out=");
		debugLines.AppendLine(FormatDouble(output));
	}

	private static string FormatDouble(double value)
	{
		return value.ToString("0.0000");
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
