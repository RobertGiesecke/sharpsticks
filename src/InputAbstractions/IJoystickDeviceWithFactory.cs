namespace SharpSticks.InputAbstractions;

public interface IJoystickDeviceWithFactory<TSelf> : IJoystickDevice
	where TSelf : JoystickDevice
{
	static abstract IJoystickDeviceFactory<TSelf> Factory { get; }
}

public static class JoystickDeviceWithFactoryExtensions
{
	extension<TSelf>(TSelf)
		where TSelf : JoystickDevice, IJoystickDeviceWithFactory<TSelf>
	{
		public static PooledList<TSelf> EnumerateConnected() => TSelf.Factory.EnumerateConnectedInputDevices();
	}
}