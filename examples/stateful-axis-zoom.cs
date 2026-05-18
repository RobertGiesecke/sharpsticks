//#:project Console/Console.csproj
#:package SharpSticks.Console@0.1.0-debug10

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
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
[assembly:RenameDevice(DeviceNames.VJoyDevice1, "VJoyLeft")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 11, "SwitchToWeaponGroup1")]
[assembly:RenameButton(DeviceNames.VJoyDevice1, 12, "SwitchToWeaponGroup2")]

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new() { Max = 1.0d },
	PrecisionCurve = new() { Max = 0.184d },
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

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	Routes =
	[
		RightStick.Buttons.CounterMeasureHatEast.ComplexRoute(new()
		{
			OnPress =
			[
				// lift fire
				VJoyLeft.Buttons.Fire.Release(),
				// switch to weapon group 2
				VJoyLeft.Buttons.SwitchToWeaponGroup2.Press(),
				WaitFor(TimeSpan.FromMilliseconds(15)),
				VJoyLeft.Buttons.SwitchToWeaponGroup2.Release(),
			],
			OnRelease =
			[
				// lift fire
				VJoyLeft.Buttons.Fire.Release(),
				// switch to weapon group 1
				VJoyLeft.Buttons.SwitchToWeaponGroup1.Press(),
				WaitFor(TimeSpan.FromMilliseconds(15)),
				VJoyLeft.Buttons.SwitchToWeaponGroup1.Release(),
			],
		}),
		RightStick.Buttons.Trigger.RouteTo(VJoyLeft.Buttons.Fire),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoyLeft.Buttons.CenterHeadTracking),
		RightStick.Axes.X.RouteToSameAxisOnOutput(VJoyLeft, options: axisOptions),
		RightStick.Axes.Y.RouteToSameAxisOnOutput(VJoyLeft, options: axisOptions),
		RightStick.Axes.Twist.RouteToSameAxisOnOutput(VJoyLeft, options: axisOptions),
	],
});
