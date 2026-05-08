using var connectedDevices = DirectInputJoystickDevice.EnumerateConnected();

var rightStick = connectedDevices.ResolveDevice("RIGHT VPC Stick WarBRD");
var leftStick = connectedDevices.ResolveDevice("LEFT VPC Stick WarBRD");

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d },
	PrecisionCurve = new AxisCurve { Max = 0.184d },
	ModifierAxis = leftStick.BindAxis(PhysicalAxis.Slider1 /*brake lever*/),
};

var blendedCurveWithPrecisionHold = new WhenButtonPressedAxisModifier
{
	Buttons = [leftStick.BindButton(2 /*2nd stage trigger*/)],
	WhenPressed = new AxisCurve { Max = 0.5d },
	WhenNotPressed = modifierBlendCurve,
};

using var runtimeMapping = Runtime.Build(new()
{
	Name = "ITB Minimal",
	VJoyDeviceId = 1,
	ConnectedDevices = [..connectedDevices],
	ButtonRoutes =
	[
		new(rightStick.BindButton(1 /*trigger*/), 1),
		new(leftStick.BindButton(1 /*trigger*/), 40),
		new(leftStick.BindButton(11 /*outer 2-way down*/), 79),
		new(rightStick.BindButton(18) /*cm hat east*/, 22),
	],
	AxisRoutes =
	[
		rightStick.BindAxis(PhysicalAxis.X).RouteToSameAxisOnVJoy(modifier: blendedCurveWithPrecisionHold),
		rightStick.BindAxis(PhysicalAxis.Y).RouteToSameAxisOnVJoy(modifier: blendedCurveWithPrecisionHold),
		rightStick.BindAxis(PhysicalAxis.Z).RouteToSameAxisOnVJoy(modifier: blendedCurveWithPrecisionHold),
	],
});

runtimeMapping.RunAsConsole();