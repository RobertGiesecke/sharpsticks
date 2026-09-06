#:package SharpSticks.Editor@0.1.0-debug05

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
[assembly: RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]

[assembly: RenameButton(DeviceNames.VJoyDevice1, 1, "FireWeapon1")]
[assembly: RenameButton(DeviceNames.VJoyDevice1, 2, "FireWeapon2")]

[assembly: RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
[assembly: RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[assembly: RenameButton(DeviceNames.RightVpcStickWarBRD, 2, "SecondStageTrigger")]


var dualCurveAxis = new WhenButtonPressedAxisModifier
{
	Buttons = [LeftVpcStickWarBRD.Buttons.Btn1],
	// Full deflection at 80% of the stick's travel; each curve runs over that range and caps at its Max.
	WhenNotPressed = new AxisCurve { Max = 1, Saturation = 0.8 /* reach 100% at 80% stick deflection */ },
	WhenPressed = new AxisCurve { Max = 0.5d, Saturation = 0.8 /* reach 100% at 80% stick deflection */ },
};

BuildAndRunAsConsole(new()
{
	Name = "Abc",
	Routes =
	[
		RightStick.Axes.X.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: dualCurveAxis),
		RightStick.Axes.Y.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: dualCurveAxis),
		RightStick.Axes.Z.RouteToSameAxisOnOutput(OutputDeviceIds.VJoyDevice1, modifier: dualCurveAxis),
		RightStick.Buttons.Trigger.RouteTo(VJoyDevice1.Buttons.FireWeapon1),
		RightStick.Buttons.SecondStageTrigger.RouteTo(VJoyDevice1.Buttons.FireWeapon2),
		RightStick.Buttons.CounterMeasureHatEast.RouteTo(VJoyDevice1.Buttons.FireWeapon2),
	],
});