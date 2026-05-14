#:project ScaledAxisCSharp.Console/ScaledAxisCSharp.Console.csproj
//#:package ScaledAxisCSharp.Console@0.1.0-debug07
using static Devices.Typed;
using static Devices;

[assembly:GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
// right stick
[assembly:RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[assembly:RenameAxis(DeviceNames.RightVpcStickWarBRD, Axis.Z, "Twist")]
[assembly:RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[assembly:RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
// left stick
[assembly:RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[assembly:RenameAxis(DeviceNames.LeftVpcStickWarBRD, Axis.Slider1, "BrakeLever")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 1, "Trigger")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 2, "SecondStageTrigger")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 11, "Outer2WayUp")]
[assembly:RenameButton(DeviceNames.LeftVpcStickWarBRD, 20, "BrakeLever")]
// vjoy device
[assembly:RenameDevice(DeviceNames.VJoyDevice1, "VJoy1")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]

[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.X, "Roll")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.Y, "Pitch")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.Z, "Yaw")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.Rz, "BrakeLever")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.Slider1, "ZoomIn")]
[assembly:RenameAxis(DeviceNames.VJoyDevice1, Axis.Slider2, "ZoomOut")]

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
		RightStick.Buttons.Trigger.RouteTo(VJoy1.Buttons.Fire),
		LeftStick.Buttons.Trigger.RouteButton(outputDeviceId: 1, 40),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoy1.Buttons.CenterHeadTracking),
		RightStick.Buttons.CounterMeasureHatEast.RouteButton(outputDeviceId: 1, 22),
		LeftStick.Buttons.BrakeLever.RouteButton(outputDeviceId: 1, 20),
		RightStick.Axes.X.RouteTo(VJoy1.Axes.Roll, modifier: blendedCurveWithPrecisionHold),
		RightStick.Axes.Y.RouteTo(VJoy1.Axes.Pitch, modifier: blendedCurveWithPrecisionHold),
		RightStick.Axes.Twist.RouteTo(VJoy1.Axes.Yaw, modifier: blendedCurveWithPrecisionHold),
		LeftStick.Axes.BrakeLever.RouteTo(VJoy1.Axes.BrakeLever, scale: 2, offset: -1),
		..LeftStick.Axes.BrakeLever.RouteAbsoluteRelative(new()
		{
			IncreaseAxis = VJoy1.Axes.ZoomIn,
			DecreaseAxis = VJoy1.Axes.ZoomOut,
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
