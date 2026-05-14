using static Devices.Typed;

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d },
	PrecisionCurve = new AxisCurve { Max = 0.184d },
	ModifierAxis = LeftStick.Axes.BrakeLever,
};

// 50% when left 2nd stage trigger is pressed, blended otherwise
var blendedCurveWithPrecisionHold = new WhenButtonPressedAxisModifier
{
	Buttons = [LeftStick.Buttons.SecondStageTrigger],
	WhenPressed = new AxisCurve { Max = 0.5d },
	WhenNotPressed = modifierBlendCurve,
};

var axisOptions = new RouteAxisOptions
{
	Modifier = blendedCurveWithPrecisionHold,
};

using var connectedDevices = DirectInputJoystickDevice.EnumerateConnected();

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	ConnectedDevices =
	[
		..connectedDevices
	],
	ButtonRoutes =
	[
		RightStick.Buttons.Trigger.RouteTo(VJoyLeft.Buttons.Fire),
		LeftStick.Buttons.Trigger.RouteButton(1, 40),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoyLeft.Buttons.CenterHeadTracking),
		RightStick.Buttons.CounterMeasureHatEast.RouteButton(1, 22),
	],
	AxisRoutes =
	[
		RightStick.Axes.X.RouteToSameAxisOnOutput(1, options: axisOptions),
		RightStick.Axes.Y.RouteToSameAxisOnOutput(1, options: axisOptions),
		RightStick.Axes.Twist.RouteToSameAxisOnOutput(1, options: axisOptions),
	],
	OutputDeviceFactory = VJoyDeviceFactory.Instance,
});


[GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
//
[RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[RenameAxis(DeviceNames.RightVpcStickWarBRD, PhysicalAxis.Z, "Twist")]
[RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
//
[RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[RenameAxis(DeviceNames.LeftVpcStickWarBRD, PhysicalAxis.Slider1, "BrakeLever")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 2, "SecondStageTrigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 11, "Outer2WayUp")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 20, "BrakeLever")]
//
[RenameDevice(DeviceNames.VJoyDevice1, "VJoyLeft")]
[RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]
// ReSharper disable once ClassNeverInstantiated.Global
partial class Devices;