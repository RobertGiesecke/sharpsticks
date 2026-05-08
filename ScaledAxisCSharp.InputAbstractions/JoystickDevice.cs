namespace ScaledAxisCSharp.InputAbstractions;

public abstract class JoystickDevice
{
	public required int DeviceId { get; init; }
	public required string Name { get; init; }
	public required string InstanceName { get; init; }
	public required JoystickCaps Caps { get; init; }
	public required WaitHandle DataAvailable { get; init; }
	public abstract bool TryRead(out JoystickState state, out string? error);
	public abstract double ReadNormalizedAxis(in JoystickState state, AxisBinding binding);
	public abstract AxisDebugSample ReadAxisDebugSample(in JoystickState state, AxisBinding binding);
}