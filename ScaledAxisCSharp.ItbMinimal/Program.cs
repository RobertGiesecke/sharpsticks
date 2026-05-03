using ScaledAxisCSharp.Config;
using ScaledAxisCSharp.DirectInput;
using ScaledAxisCSharp.VJoy;

if (!OperatingSystem.IsWindows())
{
	Console.Error.WriteLine("This tool only runs on Windows because it depends on DirectInput and vJoy.");
	return 1;
}

try
{
	return Run();
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex.Message);
	return 1;
}

int Run()
{
	var config = new ItbMinimalConfig();

	var rightStick = JoystickDevice.ResolveDevice("RIGHT VPC Stick WarBRD");
	var leftStick = JoystickDevice.ResolveDevice("LEFT VPC Stick WarBRD");

	var xAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.X, AxisMode.Signed, false, 0.0);
	var yAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.Y, AxisMode.Signed, false, 0.0);
	var zAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.Z, AxisMode.Signed, false, 0.0);
	var modifierAxis = new AxisBinding(leftStick.DeviceId, PhysicalAxis.Slider1, AxisMode.Signed, false, 0.0);

	var holdPrecisionCurve = new AxisCurve { Max = config.HoldPrecisionSlope };
	var normalCurve = new AxisCurve { Max = config.NormalSlope };
	var precisionCurve = new AxisCurve { Max = config.ModifierPrecisionSlope, };
	var modifierBlendCurve = new BlendedAxisCurve
	{
		NormalCurve = normalCurve,
		PrecisionCurve = precisionCurve,
		ModifierAxis = modifierAxis,
		ModifierMin = config.ModifierMin,
		ModifierMax = config.ModifierMax,
	};

	using var vjoy = VJoyDevice.Open(
		config.VJoyDeviceId,
		[
			new ButtonRoute(rightStick.BindButton(1), 1),
			new ButtonRoute(leftStick.BindButton(1), 40),
			new ButtonRoute(leftStick.BindButton(11), 79),
			new ButtonRoute(rightStick.BindButton(18), 22),
		],
		[
			new AxisRoute()
			{
				Source = xAxis,
				TargetAxis = VJoyAxis.X,
				Scale = 1.0,
				Offset = 0.0,
				Modifier = null
			},
			new AxisRoute()
			{
				Source = yAxis,
				TargetAxis = VJoyAxis.Y,
				Scale = 1.0,
				Offset = 0.0,
				Modifier = null
			},
			new AxisRoute()
			{
				Source = zAxis,
				TargetAxis = VJoyAxis.Z,
				Scale = 1.0,
				Offset = 0.0,
				Modifier = null
			},
		],
		[]);

	var devices = new Dictionary<int, JoystickDevice>
	{
		[rightStick.DeviceId] = rightStick,
		[leftStick.DeviceId] = leftStick,
	};

	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		// ReSharper disable once AccessToDisposedClosure
		cts.Cancel();
	};

	Console.WriteLine("Running ITB minimal profile. Press Ctrl+C to stop.");

	var currentStates = new Dictionary<int, JoystickState>(devices.Count);
	var lastReportedReadFailure = new HashSet<int>();
	var secondaryFirePrevious = false;
	var pulse71RemainingMs = 0;
	var pulse72RemainingMs = 0;

	while (!cts.IsCancellationRequested)
	{
		currentStates.Clear();
		foreach (var (deviceId, device) in devices)
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

		if (currentStates.TryGetValue(xAxis.DeviceId, out var rightState))
		{
			var holdPrecision = IsPressed(leftStick.DeviceId, 2) ||
			                    IsPressed(rightStick.DeviceId, 2) ||
			                    IsPressed(rightStick.DeviceId, 16);
			IAxisModifier stickCurve = holdPrecision ? holdPrecisionCurve : modifierBlendCurve;

			vjoy.SetAxis(VJoyAxis.X,
				stickCurve.Apply(rightStick.ReadAxisDebugSample(rightState, xAxis).NormalizedValue, currentStates,
					devices));
			vjoy.SetAxis(VJoyAxis.Y,
				stickCurve.Apply(rightStick.ReadAxisDebugSample(rightState, yAxis).NormalizedValue, currentStates,
					devices));
			vjoy.SetAxis(VJoyAxis.Z,
				stickCurve.Apply(rightStick.ReadAxisDebugSample(rightState, zAxis).NormalizedValue, currentStates,
					devices));
		}

		var primaryFire = IsPressed(rightStick.DeviceId, 1);
		var leftPrimary = IsPressed(leftStick.DeviceId, 1);
		var leftAux = IsPressed(leftStick.DeviceId, 11);
		var secondaryFire = IsPressed(rightStick.DeviceId, 18);

		vjoy.SetButton(1, primaryFire);
		vjoy.SetButton(40, leftPrimary);
		vjoy.SetButton(79, leftAux);
		vjoy.SetButton(22, secondaryFire);

		if (secondaryFire && !secondaryFirePrevious) pulse72RemainingMs = config.PulseMs;
		if (!secondaryFire && secondaryFirePrevious) pulse71RemainingMs = config.PulseMs;
		secondaryFirePrevious = secondaryFire;

		vjoy.SetButton(71, pulse71RemainingMs > 0);
		vjoy.SetButton(72, pulse72RemainingMs > 0);

		if (pulse71RemainingMs > 0) pulse71RemainingMs = Math.Max(0, pulse71RemainingMs - config.PollIntervalMs);
		if (pulse72RemainingMs > 0) pulse72RemainingMs = Math.Max(0, pulse72RemainingMs - config.PollIntervalMs);

		if (cts.Token.WaitHandle.WaitOne(config.PollIntervalMs)) break;
	}

	return 0;

	bool IsPressed(int deviceId, int button) =>
		currentStates.TryGetValue(deviceId, out var s) && s.IsButtonPressed(button);
}