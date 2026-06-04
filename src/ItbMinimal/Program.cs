[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
[assembly: RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[assembly: RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[assembly: RenameDevice(DeviceNames.VJoyDevice1, "VJoy1")]

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	Routes =
	[

		RightStick.Buttons.CounterMeasureHatEast.ComplexRoute(new()
		{
			Reentry = MacroReentry.DropIfBusy,
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
		..RightStick.Axes.X
			.GroupWith(
				RightStick.Axes.Y,
				RightStick.Axes.Twist)
			.RouteToSameAxesOnOutput(
				VJoy1,
				modifier: new BlendedAxisCurve
				{
					NormalCurve = new AxisCurve { Max = 1.0d },
					PrecisionCurve = new AxisCurve { Max = 0.184d },
					ModifierAxis = LeftStick.Axes.BrakeLever,
					Stateful = true,
				}),
		LeftStick.Axes.BrakeLever.RouteTo(VJoy1.Axes.BrakeLever, scale: 2, offset: -1),
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

// right stick
[RenameAxis(DeviceNames.RightStick, Axis.Z, "Twist")]
[RenameButton(DeviceNames.RightStick, 1, "Trigger")]
[RenameButton(DeviceNames.RightStick, 18, "CounterMeasureHatEast")]
// left stick
[RenameAxis(DeviceNames.LeftStick, Axis.Slider1, "BrakeLever")]
[RenameButton(DeviceNames.LeftStick, 1, "Trigger")]
[RenameButton(DeviceNames.LeftStick, 2, "SecondStageTrigger")]
[RenameButton(DeviceNames.LeftStick, 11, "Outer2WayUp")]
[RenameButton(DeviceNames.LeftStick, 20, "BrakeLever")]
// vjoy device 1
[RenameButton(DeviceNames.VJoy1, 1, "Fire")]
[RenameButton(DeviceNames.VJoy1, 79, "CenterHeadTracking")]
[RenameAxis(DeviceNames.VJoy1, Axis.X, "Roll")]
[RenameAxis(DeviceNames.VJoy1, Axis.Y, "Pitch")]
[RenameAxis(DeviceNames.VJoy1, Axis.Z, "Yaw")]
[RenameAxis(DeviceNames.VJoy1, Axis.Rz, "BrakeLever")]
[RenameAxis(DeviceNames.VJoy1, Axis.Slider1, "ZoomIn")]
[RenameAxis(DeviceNames.VJoy1, Axis.Slider2, "ZoomOut")]
[RenameButton(DeviceNames.VJoy1, 71, "SwitchToWeaponGroup1")]
[RenameButton(DeviceNames.VJoy1, 72, "SwitchToWeaponGroup2")]
[RenameButton(DeviceNames.VJoy1, 20, "HoldForZoom")]
[RenameButton(DeviceNames.VJoy1, 128, "Letzta")]
// ReSharper disable once ClassNeverInstantiated.Global
static partial class Devices;
