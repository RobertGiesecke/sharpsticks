using Collections.Pooled;

namespace SharpSticks.Testing;

/// <summary>
/// <see cref="IJoystickDeviceFactory{T}"/> over the manager's registered fake
/// input devices, for code paths that enumerate devices themselves instead of
/// taking a device list (e.g. the overlay server's serve path). Enumeration
/// hands out the live registered instances; the manager keeps ownership.
/// </summary>
public sealed class FakeInputDeviceFactory : IJoystickDeviceFactory<FakeJoystickDevice>
{
	private readonly FakeDeviceManager _Manager;

	internal FakeInputDeviceFactory(FakeDeviceManager manager)
	{
		_Manager = manager;
	}

	public PooledList<FakeJoystickDevice> EnumerateConnectedInputDevices()
	{
		var devices = _Manager.InputDevices;
		var list = new PooledList<FakeJoystickDevice>(devices.Length);
		foreach (var device in devices)
		{
			list.Add(device);
		}

		return list;
	}
}
