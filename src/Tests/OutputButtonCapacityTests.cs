using SharpSticks.LinuxNative;

namespace SharpSticks.Tests;

public sealed class OutputButtonCapacityTests
{
	// OutputButtonCapacity (emitted into consumers by the generator) and LinuxOutputAxisCodes both
	// hardcode their tier/ceiling values from the joystick-safe evdev ranges, and can't reference
	// each other across the emit boundary. Pin the ranges here so moving a boundary fails this test
	// and prompts updating OutputButtonCapacity (Standard/Extended/Maximum) + LinuxOutputAxisCodes.
	[Fact]
	public void TierValuesMatchTheJoystickSafeEvdevRanges()
	{
		var joystick = LinuxEventCodes.BtnDigi - LinuxEventCodes.BtnJoystick;
		var triggerHappy = LinuxEventCodes.BtnTriggerHappyEnd - LinuxEventCodes.BtnTriggerHappy;
		var misc = LinuxEventCodes.BtnMiscEnd - LinuxEventCodes.BtnMisc;

		Assert.Equal(32, joystick); // OutputButtonCapacity.Standard
		Assert.Equal(72, joystick + triggerHappy); // OutputButtonCapacity.Extended
		Assert.Equal(88, joystick + triggerHappy + misc); // OutputButtonCapacity.Maximum
	}
}
