#!/usr/bin/env dotnet

#:package SharpSticks.Editor@0.1.0-debug03

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]
[assembly: RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[assembly: RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[assembly: RenameDevice(DeviceNames.VJoyDevice1, "VJoy1")]
[assembly: RenameDevice(DeviceNames.VpcRudderPedals, "Pedals")]

var groupedZoomAxes = Pedals.Axes.RightToeBrake.GroupWith(LeftStick.Axes.BrakeLever);

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d, Exponent = 2.4d },
	PrecisionCurve = new AxisCurve { Max = 0.11d },
	// Whichever is engaged furthest wins — ModifierAxes takes the max.
	// Unsigned: both rest at the hardware minimum → factor 0 at rest.
	ModifierAxes =
	[
		..groupedZoomAxes.SourceAxes.Select(a => a with { Mode = AxisMode.Unsigned }),
	],
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
		LeftStick.Axes.BrakeLever.RouteWhenInRange(-0.95d, 1d, VJoy1.Buttons.HoldForZoom,
			options: new()
			{
				IncludeMax = true,
				Mode = AxisZoneTriggerMode.Hold,
			}),
		RightStick.Axes.X.RouteTo(VJoy1.Axes.Roll, modifier: modifierBlendCurve),
		RightStick.Axes.Y.RouteTo(VJoy1.Axes.Pitch, modifier: modifierBlendCurve),
		RightStick.Axes.Twist.RouteTo(VJoy1.Axes.Yaw, modifier: modifierBlendCurve),
		//LeftStick.Axes.BrakeLever.RouteTo(VJoy1.Axes.BrakeLever, scale: 2, offset: -1),
		// simulate absolute zoom with 2 virtual relative axes
		LeftStick.Axes.BrakeLever.RouteAbsoluteRelative(new()
		{
			IncreaseAxis = VJoy1.Axes.ZoomInOut,
			DecreaseAxis = VJoy1.Axes.ZoomInOut,
			// The lever is a signed axis resting at -1. The default source
			// range is [0, 1], which throws away the first half of the pull.
			SourceInputMinimum = -1.0,
			SourceInputMaximum = 1.0,
			Gain = 8.0,
			// Must clear the game's deadzone: pulses below it advance the
			// model but not the game, so the zoom never reaches the stops.
			// Tune to just above where the game starts reacting.
			MinOutput = 0.15,
			ErrorTolerance = 0.00003,
			IncreaseEdgeBoost = 55.5,
			DecreaseEdgeBoost = 55.5,
			// Output smoothing time (pulse 0→1) in seconds; small = snappy.
			OutputRiseSeconds = 0.2,
			OutputFallSeconds = 0.2,
			// Wall-clock: a 100% pulse drives the game's zoom fully in ~1 s.
			IncreaseSecondsToFull = 1.0,
			DecreaseSecondsToFull = 1.0,
		}),
	],
});

//pedals
[RenameAxis(DeviceNames.Pedals, Axis.Z, "Seesaw")]
[RenameAxis(DeviceNames.Pedals, Axis.Slider1, "LeftToeBrake")]
[RenameAxis(DeviceNames.Pedals, Axis.Slider2, "RightToeBrake")]
// right stick
[RenameAxis(DeviceNames.RightVpcStickWarBRD, Axis.Z, "Twist")]
[RenameButton(DeviceNames.RightVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.RightVpcStickWarBRD, 18, "CounterMeasureHatEast")]
// left stick
[RenameAxis(DeviceNames.LeftVpcStickWarBRD, Axis.Slider1, "BrakeLever")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 1, "Trigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 2, "SecondStageTrigger")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 11, "Outer2WayUp")]
[RenameButton(DeviceNames.LeftVpcStickWarBRD, 20, "BrakeLever")]
// vjoy device
[RenameButton(DeviceNames.VJoy1, 1, "Fire")]
[RenameButton(DeviceNames.VJoy1, 79, "CenterHeadTracking")]
[RenameAxis(DeviceNames.VJoy1, Axis.X, "Roll")]
[RenameAxis(DeviceNames.VJoy1, Axis.Y, "Pitch")]
[RenameAxis(DeviceNames.VJoy1, Axis.Z, "Yaw")]
[RenameAxis(DeviceNames.VJoy1, Axis.Rz, "BrakeLever")]
[RenameAxis(DeviceNames.VJoy1, Axis.Ry, "ZoomInOut")]
[RenameAxis(DeviceNames.VJoy1, Axis.Slider1, "ZoomIn")]
[RenameAxis(DeviceNames.VJoy1, Axis.Slider2, "ZoomOut")]
[RenameButton(DeviceNames.VJoy1, 71, "SwitchToWeaponGroup1")]
[RenameButton(DeviceNames.VJoy1, 72, "SwitchToWeaponGroup2")]
[RenameButton(DeviceNames.VJoy1, 20, "HoldForZoom")]
partial class Devices;