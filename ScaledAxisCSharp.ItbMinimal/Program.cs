using var connectedDevices = JoystickDevice.EnumerateConnected();

var rightStick = connectedDevices.ResolveDevice("RIGHT VPC Stick WarBRD");
var leftStick = connectedDevices.ResolveDevice("LEFT VPC Stick WarBRD");

var xAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.X, AxisMode.Signed);
var yAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.Y, AxisMode.Signed);
var zAxis = new AxisBinding(rightStick.DeviceId, PhysicalAxis.Z, AxisMode.Signed);
var modifierAxis = new AxisBinding(leftStick.DeviceId, PhysicalAxis.Slider1, AxisMode.Signed);

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d },
	PrecisionCurve = new AxisCurve { Max = 0.184d },
	ModifierAxis = modifierAxis,
};

var holdPrecisionCurve = new AxisCurve { Max = 0.5d };

var blendedCurveWithPrecisionHold = new WhenButtonPressedAxisModifier
{
	Buttons =
	[
		leftStick.BindButton(2),
		rightStick.BindButton(2),
		rightStick.BindButton(16),
	],
	WhenPressed = holdPrecisionCurve,
	WhenNotPressed = modifierBlendCurve,
};

using var runtimeMapping = Runtime.Build(new()
{
	VJoyDeviceId = 1,
	PollIntervalMs = 5,
	ConnectedDevices = [..connectedDevices],
	ButtonRoutes =
	[
		new(rightStick.BindButton(1), 1),
		new(leftStick.BindButton(1), 40),
		new(leftStick.BindButton(11), 79),
		new(rightStick.BindButton(18), 22),
	],
	AxisRoutes =
	[
		new() { Source = xAxis, TargetAxis = VJoyAxis.X, Modifier = blendedCurveWithPrecisionHold, },
		new() { Source = yAxis, TargetAxis = VJoyAxis.Y, Modifier = blendedCurveWithPrecisionHold, },
		new() { Source = zAxis, TargetAxis = VJoyAxis.Z, Modifier = blendedCurveWithPrecisionHold, },
	],
});

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	// ReSharper disable once AccessToDisposedClosure
	cts.Cancel();
};

Console.WriteLine("Running ITB minimal profile. Press Ctrl+C to stop.");

runtimeMapping.Run(cts.Token);