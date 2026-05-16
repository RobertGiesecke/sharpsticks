namespace ScaledAxisCSharp.InputAbstractions;

public abstract class JoystickDevice : IDisposable
{
	public required int DeviceId { get; init; }
	public required string Name { get; init; }
	public required string InstanceName { get; init; }
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