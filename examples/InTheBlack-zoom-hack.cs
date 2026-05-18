// #:project Console/Console.csproj
#:package SharpSticks.Console@0.1.0-debug01

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new() { Max = 1.0d },
	PrecisionCurve = new() { Max = 0.2d },
	ModifierAxis = LeftStick.Axes.BrakeLever,
	Stateful = true,
};

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	Routes =
	[
		// switch to gimbals while holding cm hat east
		RightStick.Buttons.CounterMeasureHatEast.ComplexRoute(new()
		{
			OnPress =
			[
				// lift fire
				VJoy1.Buttons.Fire.Release(),
				// switch to weapon group 2
				VJoy1.Buttons.SwitchToWeaponGroup2.Press(),
				WaitFor(TimeSpan.FromMilliseconds(15)),
				VJoy1.Buttons.SwitchToWeaponGroup2.Release(),
			],
			OnRelease =
			[
				// lift fire
				VJoy1.Buttons.Fire.Release(),
				// switch to weapon group 1
				VJoy1.Buttons.SwitchToWeaponGroup1.Press(),
				WaitFor(TimeSpan.FromMilliseconds(15)),
				VJoy1.Buttons.SwitchToWeaponGroup1.Release(),
			],
		}),
		RightStick.Buttons.Trigger.RouteTo(VJoy1.Buttons.Fire),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoy1.Buttons.CenterHeadTracking),
		LeftStick.Buttons.BrakeLever.RouteTo(VJoy1.Buttons.HoldForZoom),
		RightStick.Axes.X.RouteTo(VJoy1.Axes.Roll, modifier: modifierBlendCurve),
		RightStick.Axes.Y.RouteTo(VJoy1.Axes.Pitch, modifier: modifierBlendCurve),
		RightStick.Axes.Twist.RouteTo(VJoy1.Axes.Yaw, modifier: modifierBlendCurve),
		LeftStick.Axes.BrakeLever.RouteTo(VJoy1.Axes.BrakeLever, scale: 2, offset: -1),
		// simulate absolute zoom with 2 virtual relative axes
		..LeftStick.Axes.BrakeLever.RouteAbsoluteRelative(new()
		{
			IncreaseAxis = VJoy1.Axes.ZoomIn,
			DecreaseAxis = VJoy1.Axes.ZoomOut,
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
// right stick
[RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[RenameAxis(DeviceNames.RightVpcStickWarBRD, Axis.Z, "Twist")]

[RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
// left stick
[RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[RenameAxis(DeviceNames.LeftVpcStickWarBRD, Axis.Slider1, "BrakeLever")]

[RenameButton(DeviceNames.LeftVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 2, "SecondStageTrigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 11, "Outer2WayUp")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 20, "BrakeLever")]
// vjoy device
[RenameDevice(DeviceNames.VJoyDevice1, "VJoy1")]
[RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]

[RenameAxis(DeviceNames.VJoyDevice1, Axis.X, "Roll")]
[RenameAxis(DeviceNames.VJoyDevice1, Axis.Y, "Pitch")]
[RenameAxis(DeviceNames.VJoyDevice1, Axis.Z, "Yaw")]
[RenameAxis(DeviceNames.VJoyDevice1, Axis.Rz, "BrakeLever")]
[RenameAxis(DeviceNames.VJoyDevice1, Axis.Slider1, "ZoomIn")]
[RenameAxis(DeviceNames.VJoyDevice1, Axis.Slider2, "ZoomOut")]

[RenameButton(DeviceNames.VJoyDevice1, 71, "SwitchToWeaponGroup1")]
[RenameButton(DeviceNames.VJoyDevice1, 72, "SwitchToWeaponGroup2")]
[RenameButton(DeviceNames.VJoyDevice1, 20, "HoldForZoom")]
partial class Devices;