using System.Collections.Immutable;
using SharpSticks.InputSynthesis.Mouse;
using static System.TimeSpan;

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]

#if WINDOWS
[assembly: RenameDevice(DeviceNames.RightVpcStickWarBRD, "RightStick")]
[assembly: RenameDevice(DeviceNames.LeftVpcStickWarBRD, "LeftStick")]
[assembly: RenameDevice(DeviceNames.VJoyDevice1, "VJoy1")]
[assembly: RenameDevice(DeviceNames.VpcRudderPedals, "Pedals")]
#elif LINUX
[assembly: OutputDevice(1,
    CodeName = "VJoy1",
    Axes =
    [
        Axis.X,
        Axis.Y,
        Axis.Z,
        Axis.Rx,
        Axis.Ry,
        Axis.Rz,
        Axis.Slider1,
        Axis.Slider2,
    ],
    ButtonCapacity = OutputButtonCapacity.Maximum)]
[assembly: RenameDevice(DeviceNames.VirpilControls20220407RVpcStickWarBRD, "RightStick")]
[assembly: RenameDevice(DeviceNames.VirpilControls20220407LVpcStickWarBRD, "LeftStick")]
[assembly: RenameDevice(DeviceNames.VirpilControls20220407VpcRudderPedals, "Pedals")]
#endif

var VJoy1 = Typed.VJoyDevice;

var groupedZoomAxes = Pedals.Axes.RightToeBrake
	.GroupWith(LeftStick.Axes.BrakeLever)
	.WithAxisMode(AxisMode.Unsigned);

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d, Exponent = 2.4d },
	PrecisionCurve = new AxisCurve { Max = 0.05d },
	// Whichever is engaged the furthest wins — ModifierAxes takes the max.
	// Unsigned: both rest at the hardware minimum → factor 0 at rest.
	ModifierAxes =
	[
		.. groupedZoomAxes.SourceAxes,
	],
	Stateful = true,
};

BuildAndRunAsConsole(new()
{
	Name = "ItB minimal + scaled rotations",
	Routes =
	[
		RightStick.Axes.Rx.RouteToMouse(MouseDirection.X, sensitivity: 2000),
		RightStick.Axes.Ry.RouteToMouse(MouseDirection.Y, sensitivity: 2000),
		RightStick.Buttons.ThumbStick.RouteTo(MouseOutput.Buttons.Left),
		// switch to gimbals while holding cm hat east
		RightStick.Buttons.CounterMeasureHatEast.ComplexRoute(new()
		{
			OnPress =
			[
				// lift fire
				VJoy1.Buttons.Fire.Release(),
				// switch to weapon group 2
				VJoy1.Buttons.SwitchToWeaponGroup2.Press(),
				WaitFor(FromMilliseconds(15)),
				VJoy1.Buttons.SwitchToWeaponGroup2.Release(),
			],
			OnRelease =
			[
				// lift fire
				VJoy1.Buttons.Fire.Release(),
				// switch to weapon group 1
				VJoy1.Buttons.SwitchToWeaponGroup1.Press(),
				WaitFor(FromMilliseconds(15)),
				VJoy1.Buttons.SwitchToWeaponGroup1.Release(),
			],
		}),
		RightStick.Buttons.Trigger.RouteTo(VJoy1.Buttons.Fire),
		LeftStick.Buttons.Outer2WayUp.RouteTo(VJoy1.Buttons.CenterHeadTracking),
		.. LeftStick.Axes.BrakeLever.RouteWhenInRange(-0.95d, 1d, VJoy1.Buttons.HoldForZoom,
				options: new()
				{
					IncludeMax = true,
					Mode = AxisZoneTriggerMode.Hold,
				}) switch
			{
				var x => ImmutableArray.Create(x, x with
				{
					Target = VJoy1.Buttons.HoldWhenNotZoomed,
					Inverted = true,
				}),
			},
		RightStick.Axes.X.RouteTo(VJoy1.Axes.Roll, modifier: modifierBlendCurve),
		RightStick.Axes.Y.RouteTo(VJoy1.Axes.Pitch, modifier: modifierBlendCurve),
		RightStick.Axes.Twist.RouteTo(VJoy1.Axes.Yaw, modifier: modifierBlendCurve),

		LeftStick.Axes.BrakeLever.RouteTo(VJoy1.Axes.BrakeLever /*, scale: 2, offset: -1*/),
		Pedals.Axes.RightToeBrake.RouteTo(VJoy1.Axes.RightToeBrake, scale: 2, offset: -1),
		// simulate absolute zoom with 2 virtual relative axes
		LeftStick.Axes.BrakeLever.RouteAbsoluteRelative(new()
		{
			IncreaseAxis = VJoy1.Axes.ZoomInOut,
			DecreaseAxis = VJoy1.Axes.ZoomInOut,
			// The lever is a signed axis resting at -1. The default source
			// range is [0, 1], which throws away the first half of the pull.
			SourceInputMinimum = -1.0,
			SourceInputMaximum = 1.0,
			Gain = 5.0,
			// Must clear the game's deadzone: pulses below it advance the
			// model but not the game, so the zoom never reaches the stops.
			// Tune to just above where the game starts reacting.
			MinOutput = 0.015,
			// One visible HUD step is roughly 1/124 of the rail. Settling inside
			// that avoids a continuous end-of-travel hunt for invisible movement.
			ErrorTolerance = 0.01,
			// Pin the lever at a rail → drive a full pulse that way for this
			// long, so the game is slammed to the stop and mirrors the lever.
			// Set ≥ the *TimeToFull below (the game's full-travel time).
			IncreaseEdgeHoldTime = FromSeconds(1.4),
			DecreaseEdgeHoldTime = FromSeconds(1.25),
			// Output smoothing time (pulse 0→1); small = snappy.
			OutputRiseTime = FromSeconds(0.05),
			OutputFallTime = FromSeconds(0.05),
			// Provisional values. The feedback sweep must own vJoy exclusively
			// before it can validate replacement full-travel timings.
			IncreaseTimeToFull = FromSeconds(1.18),
			DecreaseTimeToFull = FromSeconds(1.08),
		}),
	],
});


//pedals
[RenameAxis(DeviceNames.Pedals, Axis.Z, "Seesaw")]
[RenameAxis(DeviceNames.Pedals, Axis.Slider1, SharedNames.LeftToeBrake)]
[RenameAxis(DeviceNames.Pedals, Axis.Slider2, SharedNames.RightToeBrake)]
// right stick
[RenameAxis(DeviceNames.RightStick, Axis.Z, "Twist")]
[RenameButton(DeviceNames.RightStick, 1, "Trigger")]
[RenameButton(DeviceNames.RightStick, 18, "CounterMeasureHatEast")]
[RenameButton(DeviceNames.RightStick, 6, "ThumbStick")]
// left stick
[RenameAxis(DeviceNames.LeftStick, Axis.Slider1, SharedNames.BrakeLever)]
[RenameButton(DeviceNames.LeftStick, 1, "Trigger")]
[RenameButton(DeviceNames.LeftStick, 2, "SecondStageTrigger")]
[RenameButton(DeviceNames.LeftStick, 11, "Outer2WayUp")]
[RenameButton(DeviceNames.LeftStick, 20, SharedNames.BrakeLever)]
// vjoy device
[RenameButton(DeviceNames.VJoyDevice, 1, "Fire")]
[RenameButton(DeviceNames.VJoyDevice, 79, "CenterHeadTracking")]
[RenameAxis(DeviceNames.VJoyDevice, Axis.X, "Roll")]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Y, "Pitch")]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Z, "Yaw")]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Rz, SharedNames.BrakeLever)]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Rx, SharedNames.RightToeBrake)]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Ry, SharedNames.ZoomInOut)]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Slider1, "ZoomIn")]
[RenameAxis(DeviceNames.VJoyDevice, Axis.Slider2, "ZoomOut")]
[RenameButton(DeviceNames.VJoyDevice, 71, "SwitchToWeaponGroup1")]
[RenameButton(DeviceNames.VJoyDevice, 72, "SwitchToWeaponGroup2")]
[RenameButton(DeviceNames.VJoyDevice, 20, "HoldForZoom")]
[RenameButton(DeviceNames.VJoyDevice, 21, "HoldWhenNotZoomed")]
partial class Devices;

static class SharedNames
{
	public const string BrakeLever = "BrakeLever";
	public const string RightToeBrake = "RightToeBrake";
	public const string LeftToeBrake = "LeftToeBrake";
	public const string ZoomInOut = "ZoomInOut";
}
