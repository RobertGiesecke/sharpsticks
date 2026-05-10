using var connectedDevices = DirectInputJoystickDevice.EnumerateConnected();

var rightStick = connectedDevices.ResolveDevice("RIGHT VPC Stick WarBRD");
var leftStick = connectedDevices.ResolveDevice("LEFT VPC Stick WarBRD");

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d },
	PrecisionCurve = new AxisCurve { Max = 0.184d },
	ModifierAxis = leftStick.BindAxis(PhysicalAxis.Slider1 /*brake lever*/),
};

// 50% when left 2nd stage trigger is pressed, blended otherwise
var blendedCurveWithPrecisionHold = new WhenButtonPressedAxisModifier
{
	Buttons = [leftStick.BindButton(2 /*2nd stage trigger*/)],
	WhenPressed = new AxisCurve { Max = 0.5d },
	WhenNotPressed = modifierBlendCurve,
};

var axisOptions = new RouteAxisOptions
{
	Modifier = blendedCurveWithPrecisionHold,
};

Runtime.BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	ConnectedDevices =
	[
		..connectedDevices
	],
	ButtonRoutes =
	[
		new(rightStick.BindButton(1 /*trigger*/),
			1,
			1),
		new(leftStick.BindButton(1 /*trigger*/),
			1,
			40),
		new(leftStick.BindButton(11 /*outer 2-way up*/),
			1,
			79),
		new(rightStick.BindButton(18) /*cm hat east*/,
			1,
			22),
	],
	AxisRoutes =
	[
		rightStick.BindAxis(PhysicalAxis.X)
			.RouteToSameAxisOnOutput(1,
				options: axisOptions),
		rightStick.BindAxis(PhysicalAxis.Y)
			.RouteToSameAxisOnOutput(1,
				options: axisOptions),
		rightStick.BindAxis(PhysicalAxis.Z)
			.RouteToSameAxisOnOutput(1,
				options: axisOptions),
	],
	OutputDeviceFactory = VJoyDeviceFactory.Instance,
});