#:project ScaledAxisCSharp.Console/ScaledAxisCSharp.Console.csproj
//#:package ScaledAxisCSharp.Console@0.1.0-debug05
using static Devices.Typed;
using static Devices;

[assembly:GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
//
[assembly:RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[assembly:RenameAxis(DeviceNames.RightVpcStickWarBRD, PhysicalAxis.Z, "Twist")]
[assembly:RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[assembly:RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
//
[assembly:RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[assembly:RenameAxis(DeviceNames.LeftVpcStickWarBRD, PhysicalAxis.Slider1, "BrakeLever")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 1, "Trigger")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 2, "SecondStageTrigger")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 11, "Outer2WayUp")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 20, "BrakeLever")]
//
[assembly:RenameDevice(DeviceNames.VJoyDevice1, "VJoyLeft")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]

[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.X, "Roll")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Y, "Pitch")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Z, "Yaw")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Rz, "BrakeLever")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Slider1, "ZoomIn")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Slider2, "ZoomOut")]


using var connectedDevices = EnumerateConnectedDevices();

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

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	ConnectedDevices = [.. connectedDevices],
	Routes =
	[
		RightStick.Buttons.Trigger.RouteTo(VJoyLeft.Buttons.Fire),
		LeftStick.Buttons.Trigger.RouteButton(outputDeviceId: 1, 40),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoyLeft.Buttons.CenterHeadTracking),
		RightStick.Buttons.CounterMeasureHatEast.RouteButton(outputDeviceId: 1, 22),
		LeftStick.Buttons.BrakeLever.RouteButton(outputDeviceId: 1, 20),
		RightStick.Axes.X.RouteTo(VJoyLeft.Axes.Roll, modifier: blendedCurveWithPrecisionHold),
		RightStick.Axes.Y.RouteTo(VJoyLeft.Axes.Pitch, modifier: blendedCurveWithPrecisionHold),
		RightStick.Axes.Twist.RouteTo(VJoyLeft.Axes.Yaw, modifier: blendedCurveWithPrecisionHold),
		LeftStick.Axes.BrakeLever.RouteTo(VJoyLeft.Axes.BrakeLever, scale: 2, offset: -1),
		..LeftStick.Axes.BrakeLever.RouteAbsoluteRelative(new()
		{
			IncreaseAxis = VJoyLeft.Axes.ZoomIn,
			DecreaseAxis = VJoyLeft.Axes.ZoomOut,
			IncreaseRestPosition = 0.5,
			DecreaseRestPosition = 0.5,
			InitialValue = 0.0,
			Gain = 8.0,
			MinOutput = 0.001,
			ErrorTolerance = 0.00003,
			IncreaseEdgeBoost = 5.5,
			DecreaseEdgeBoost = 5.5,
			OutputRiseRate = 0.14,
			OutputFallRate = 0.14,
			IncreaseRate = 0.035,
			DecreaseRate = 0.035,
		}),
	],
});
