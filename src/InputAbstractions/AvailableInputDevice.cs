namespace SharpSticks.InputAbstractions;

/// Design-time / pre-acquire snapshot of an input device. Factories surface these via
/// <see cref="IJoystickDeviceFactory.EnumerateAvailableInputs"/> without claiming the
/// device, so source generators and tooling can read capabilities cheaply.
public readonly record struct AvailableInputDevice(
	int DeviceId,
	string ProductName,
	Guid ProductGuid,
	ImmutableArray<Axis> Axes,
	uint ButtonCount);
