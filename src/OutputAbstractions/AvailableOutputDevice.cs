namespace SharpSticks.OutputAbstractions;

/// Design-time / pre-acquire snapshot of an output slot. Factories surface these via
/// <see cref="IOutputDeviceFactory.EnumerateAvailableOutputs"/> without claiming the
/// slot. <paramref name="InputProductGuid"/> identifies the product GUID of the
/// input-side shadow each output device surfaces as (vJoy PIDVID on Windows; the
/// per-uinput VID/PID encoding on Linux), or <see cref="Guid.Empty"/> when none.
public readonly record struct AvailableOutputDevice(
	uint DeviceId,
	ImmutableArray<Axis> Axes,
	uint ButtonCount,
	Guid InputProductGuid);
