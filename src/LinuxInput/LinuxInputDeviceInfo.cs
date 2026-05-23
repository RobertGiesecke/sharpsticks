namespace SharpSticks.LinuxInput;

/// Metadata for a detected evdev device that we recognized as a joystick / gamepad.
public readonly record struct LinuxInputDeviceInfo(
	int DeviceId,
	string EventPath,
	string ProductName,
	string InstanceName,
	Guid InstanceGuid,
	ImmutableArray<Axis> Axes,
	ImmutableArray<ushort> ButtonCodes);
