[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]

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
				Macros.Release(VJoyLeft.Buttons.Fire),
				// switch to weapon group 2
				Macros.Press(VJoyLeft.Buttons.SwitchToWeaponGroup2),
				Macros.Wait(TimeSpan.FromMilliseconds(15)),
				Macros.Release(VJoyLeft.Buttons.SwitchToWeaponGroup2),
			],
			OnRelease =
			[
				// lift fire
				Macros.Release(VJoyLeft.Buttons.Fire),
				// switch to weapon group 1
				Macros.Press(VJoyLeft.Buttons.SwitchToWeaponGroup1),
				Macros.Wait(TimeSpan.FromMilliseconds(15)),
				Macros.Release(VJoyLeft.Buttons.SwitchToWeaponGroup1),
			],
		}),
		RightStick.Buttons.Trigger.RouteTo(VJoyLeft.Buttons.Fire),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoyLeft.Buttons.CenterHeadTracking),
		RightStick.Axes.X.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyLeft, options: axisOptions),
		RightStick.Axes.Y.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyLeft, options: axisOptions),
		RightStick.Axes.Twist.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyLeft, options: axisOptions),
	],
	OutputDeviceFactory = PlatformDefaultOutputDeviceFactory.Instance,
});

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
[RenameDevice(DeviceNames.VJoyDevice1, "VJoyLeft")]
[RenameButton(DeviceNames.VJoyDevice1, 1, "Fire")]
[RenameButton(DeviceNames.VJoyDevice1, 79, "CenterHeadTracking")]
[RenameButton(DeviceNames.VJoyDevice1, 11, "SwitchToWeaponGroup1")]
[RenameButton(DeviceNames.VJoyDevice1, 12, "SwitchToWeaponGroup2")]
// ReSharper disable once ClassNeverInstantiated.Global
partial class Devices;