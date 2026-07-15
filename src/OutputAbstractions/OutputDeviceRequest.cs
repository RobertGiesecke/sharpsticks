namespace SharpSticks.OutputAbstractions;

/// One device the runtime wants the output factory to create or acquire. Bundled into a
/// batch passed to <see cref="IOutputDeviceFactory.EnumerateConnectedOutputDevices"/> so the factory can match each
/// created output to its input counterpart in a single pass without rebuilding state per
/// device.
public readonly record struct OutputDeviceRequest(
	uint DeviceId,
	IReadOnlyCollection<OutputButtonBinding> OutputButtons,
	IReadOnlyCollection<AxisRoute> AxisRoutes,
	IReadOnlyCollection<int> MacroButtonNumbers)
{
	/// Number of buttons to materialize: the max of the declared [OutputDevice] count and the
	/// highest routed button. 0 (the default) means nothing was declared — the factory then
	/// falls back to creating exactly the routed buttons.
	public uint ButtonCount { get; init; }

	/// Declared axis set from [OutputDevice], so axes the device advertises but no route drives
	/// are still created. Default (empty) when nothing is declared.
	public ImmutableArray<Axis> DeclaredAxes { get; init; }
}
