namespace SharpSticks.InputAbstractions;

public interface IJoystickDevice : IInputDevice
{
	int DeviceId { get; }
}