#!/usr/bin/env dotnet

//#:package SharpSticks.Editor@0.1.0-debug04
#:project ../src/Editor/Editor.csproj
#:project ../src/Overlay.Integration/Overlay.Integration.csproj

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

var zoomProfile = ZoomProfile.FromEnvironment();
Console.WriteLine(
	$"Zoom profile: {zoomProfile.Name} (set SHARPSTICKS_ZOOM_PROFILE to low-latency, balanced, or smooth)");

var groupedZoomAxes = Pedals.Axes.RightToeBrake
	.GroupWith(LeftStick.Axes.BrakeLever)
	.WithAxisMode(AxisMode.Unsigned);

var modifierBlendCurve = new BlendedAxisCurve
{
	NormalCurve = new AxisCurve { Max = 1.0d, Exponent = 1.8d },
	PrecisionCurve = new AxisCurve { Max = 0.01d },
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
	RunEventFactory = f =>
	[
		f.ServeOverlay(new()
		{
			WebRoot = @"c:\tools\joystick-overlay",
			WebRootPath = "joyviz.html",
			Port = 8787,
		}),
	],
	Routes =
	[
		RightStick.Axes.ThumbStickHorizontal.RouteToMouse(MouseDirection.X, sensitivity: 2000),
		RightStick.Axes.ThumbStickVertical.RouteToMouse(MouseDirection.Y, sensitivity: 2000),
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
		// Leave the first 5% of lever travel completely unzoomed. Beyond -0.9,
		// the remaining physical travel maps continuously onto 0..100% zoom.
		.. LeftStick.Axes.BrakeLever.RouteWhenInRange(-0.9d, 1d, VJoy1.Buttons.HoldForZoom,
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
			SourceInputMinimum = -0.9,
			SourceInputMaximum = 1.0,
			Gain = zoomProfile.Gain,
			// Feed the requested target velocity forward immediately; Gain then
			// corrects residual position error instead of trailing smooth pulls.
			TargetVelocityFeedForward = zoomProfile.TargetVelocityFeedForward,
			TargetVelocitySmoothingTimeConstant = zoomProfile.TargetVelocitySmoothingTimeConstant,
			TargetVelocityDeadband = zoomProfile.TargetVelocityDeadband,
			TargetPositionSmoothingTimeConstant = zoomProfile.TargetPositionSmoothingTimeConstant,
			SuppressOpposingPulseUntilSourceReverses = true,
			DirectionReversalBoostTime = zoomProfile.DirectionReversalBoostTime,
			// Fitted from direct fixed-pulse tests, independently of the tracking
			// controller. Pulses below about 0.07 produced no visible HUD motion.
			IncreaseResponseDeadzone = 0.042,
			DecreaseResponseDeadzone = 0.041,
			IncreaseResponseExponent = 0.95,
			DecreaseResponseExponent = 0.84,
			// Do not model response inertia here. Predicting coast after the lever
			// stops causes an opposite correction and a visible backward bounce.
			IncreaseResponseTimeConstant = TimeSpan.Zero,
			DecreaseResponseTimeConstant = TimeSpan.Zero,
			// Clear the larger directional pickup deadzone when a correction is
			// required. One normalized visible meter step was about 0.00245.
			MinOutput = zoomProfile.MinOutput,
			ErrorTolerance = zoomProfile.ErrorTolerance,
			// Pin the lever at a rail → drive a full pulse that way for this
			// long, so the game is slammed to the stop and mirrors the lever.
			// Set ≥ the *TimeToFull below (the game's full-travel time).
			IncreaseEdgeHoldTime = FromSeconds(0.75),
			DecreaseEdgeHoldTime = FromSeconds(0.75),
			OutputRiseTime = zoomProfile.OutputRiseTime,
			OutputFallTime = zoomProfile.OutputFallTime,
			// Step-and-hold calibration: these deliberately advance the internal
			// model faster than the earlier full-pulse fit, which otherwise kept the
			// minimum pulse active and drove through every requested position.
			IncreaseTimeToFull = FromSeconds(0.900),
			DecreaseTimeToFull = FromSeconds(0.700),
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
[RenameButton(DeviceNames.RightStick, 6, SharedNames.ThumbStick)]
[RenameAxis(DeviceNames.RightStick, Axis.Rx, SharedNames.ThumbStickHorizontal)]
[RenameAxis(DeviceNames.RightStick, Axis.Ry, SharedNames.ThumbStickVertical)]
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
	public const string ThumbStick = "ThumbStick";
	public const string ThumbStickHorizontal = $"{ThumbStick}Horizontal";
	public const string ThumbStickVertical = $"{ThumbStick}Vertical";
	public const string RightToeBrake = "RightToeBrake";
	public const string LeftToeBrake = "LeftToeBrake";
	public const string ZoomInOut = "ZoomInOut";
}

readonly record struct ZoomProfile(
	string Name,
	double Gain,
	double TargetVelocityFeedForward,
	TimeSpan TargetVelocitySmoothingTimeConstant,
	double TargetVelocityDeadband,
	TimeSpan TargetPositionSmoothingTimeConstant,
	double MinOutput,
	double ErrorTolerance,
	TimeSpan OutputRiseTime,
	TimeSpan OutputFallTime,
	TimeSpan DirectionReversalBoostTime)
{
	public static ZoomProfile FromEnvironment()
	{
		var name = Environment.GetEnvironmentVariable("SHARPSTICKS_ZOOM_PROFILE") ?? "balanced";
		return name.Trim().ToLowerInvariant() switch
		{
			"low-latency" or "latency" => new("low-latency", 5.0, 1.0, FromMilliseconds(5), 0.005, TimeSpan.Zero, 0.070,
				0.0032,
				TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
			"balanced" => new("balanced", 5.0, 1.0, FromMilliseconds(10), 0.005, FromMilliseconds(25), 0.070, 0.0032,
				FromMilliseconds(8), FromMilliseconds(12), TimeSpan.Zero),
			"smooth" or "smoothness" => new("smooth", 5.0, 1.0, FromMilliseconds(20), 0.005, FromMilliseconds(60),
				0.070, 0.0032,
				FromMilliseconds(20), FromMilliseconds(30), TimeSpan.Zero),
			_ => throw new ArgumentException(
				$"Unknown SHARPSTICKS_ZOOM_PROFILE '{name}'. Use low-latency, balanced, or smooth."),
		};
	}
}
