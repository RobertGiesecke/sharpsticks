namespace SharpSticks.OutputAbstractions;

/// One device the runtime wants the output factory to create or acquire. Bundled into a
/// batch passed to <see cref="IOutputDeviceFactory.EnumerateConnectedOutputDevices"/> so the factory can match each
/// created output to its input counterpart in a single pass without rebuilding state per
/// device.
public readonly record struct OutputDeviceRequest(
	uint DeviceId,
	IReadOnlyCollection<OutputButtonBinding> OutputButtons,
	IReadOnlyCollection<AxisRoute> AxisRoutes,
	IReadOnlyCollection<int> MacroButtonNumbers);
