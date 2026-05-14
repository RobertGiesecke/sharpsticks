//#:project ScaledAxisCSharp.Console/ScaledAxisCSharp.Console.csproj
#:package ScaledAxisCSharp.Console@0.1.0-debug04

using static Devices.Typed;

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
	ButtonRoutes =
	[
		RightStick.Buttons.Trigger.RouteTo(VJoyLeft.Buttons.Fire),
		LeftStick.Buttons.Trigger.RouteButton(outputDeviceId: 1, 40),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoyLeft.Buttons.CenterHeadTracking),
		RightStick.Buttons.CounterMeasureHatEast.RouteButton(outputDeviceId: 1, 22),
		LeftStick.Buttons.BrakeLever.RouteButton(outputDeviceId: 1, 20),
	],
	AxisRoutes =
	[
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

[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.X, "Roll")]
[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Y, "Pitch")]
[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Z, "Yaw")]
[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Rz, "BrakeLever")]
[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Slider1, "ZoomIn")]
[RenameAxis(DeviceNames.VJoyDevice1, PhysicalAxis.Slider2, "ZoomOut")]
// ReSharper disable once ClassNeverInstantiated.Global
partial class Devices;
