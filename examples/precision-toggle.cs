#:package SharpSticks.Editor@0.1.0-debug05

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
[assembly: RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]

[assembly: RenameButton(DeviceNames.VJoyDevice1, 1, "FireWeapon1")]
[assembly: RenameButton(DeviceNames.VJoyDevice1, 2, "FireWeapon2")]

[assembly: RenameButton(DeviceNames.RightStick, 18, "CounterMeasureHatEast")]
[assembly: RenameButton(DeviceNames.RightStick, 1, "Trigger")]
[assembly: RenameButton(DeviceNames.RightStick, 2, "SecondStageTrigger")]


var axisWith80PercentSaturation = new AxisCurve { Max = 1, Saturation = 0.8 /* reach 100% at 80% stick deflection */ };
var dualCurveAxis = new WhenButtonPressedAxisModifier
{
	Buttons = [LeftVpcStickWarBRD.Buttons.Btn1],
	// Full deflection at 80% of the stick's travel; each curve runs over that range and caps at its Max.
	WhenNotPressed = axisWith80PercentSaturation,
	WhenPressed = new AxisCurve { Max = 0.5d },
};

BuildAndRunAsConsole(new()
{
	Name = "Abc",
	Routes =
	[
		RightStick.Axes.X.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: dualCurveAxis),
		RightStick.Axes.Y.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: dualCurveAxis),
		RightStick.Axes.Z.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: axisWith80PercentSaturation),
		RightStick.Buttons.Trigger.RouteTo(VJoyDevice1.Buttons.FireWeapon1),
		RightStick.Buttons.SecondStageTrigger.RouteTo(VJoyDevice1.Buttons.FireWeapon2),
		RightStick.Buttons.CounterMeasureHatEast.RouteTo(VJoyDevice1.Buttons.FireWeapon2),
	],
});