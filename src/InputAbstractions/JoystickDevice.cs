namespace SharpSticks.InputAbstractions;

public abstract class JoystickDevice : IDisposable, IJoystickDevice
{
	public required int DeviceId { get; init; }
	public required string Name { get; init; }
	public required string InstanceName { get; init; }

	/// <summary>
	/// Per-instance identity. Platform-supplied stable id for this specific
	/// plugged-in device: on Windows that's DirectInput's <c>guidInstance</c>;
	/// on Linux a future impl would synthesize one (SDL-style) from
	/// bus/vendor/product/serial. Survives Windows reassigning
	/// <see cref="DeviceId"/> between sessions.
	/// </summary>
	public required Guid InstanceGuid { get; init; }

	/// <summary>
	/// Per-hardware-kind identity. DirectInput's <c>guidProduct</c> (PIDVID-encoded
	/// VID/PID) on Windows; the same encoding synthesised from
	/// <c>input_id.vendor</c>/<c>input_id.product</c> on Linux via
	/// <see cref="ProductGuidEncoder"/>. Two physically identical devices share this
	/// Guid; use <see cref="InstanceGuid"/> for per-instance identity.
	/// </summary>
	public required Guid ProductGuid { get; init; }

	public required JoystickCapabilities Capabilities { get; init; }
	public required ImmutableArray<Axis> PhysicalAxes { get; init; }
	public required WaitHandle DataAvailable { get; init; }
	public abstract bool TryReadState(out JoystickState state, out string? error);
	public abstract double ReadNormalizedAxisValue(in JoystickState state, AxisBinding binding);
	public abstract AxisDebugSample ReadAxisDebugSample(in JoystickState state, AxisBinding binding);

	public virtual void Dispose()
	{
	}
}