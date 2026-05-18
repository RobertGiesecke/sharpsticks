using Collections.Pooled;

namespace SharpSticks.Testing;

/// <summary>
/// <see cref="IOutputDeviceFactory"/> that hands out pre-registered
/// <see cref="FakeOutputDevice"/> instances. Tests typically populate this
/// indirectly via <see cref="FakeDeviceManager.AddOutputDevice"/>; opening an
/// id that wasn't registered throws so routing typos fail fast.
/// </summary>
public sealed class FakeOutputDeviceFactory : IOutputDeviceFactory, IDisposable
{
	private readonly PooledDictionary<uint, FakeOutputDevice> _Devices = new();

	public FakeOutputDevice this[uint deviceId] => Get(deviceId);

	public FakeOutputDevice Get(uint deviceId) =>
		_Devices.TryGetValue(deviceId, out var device)
			? device
			: throw new InvalidOperationException(
				$"No fake output device registered for id {deviceId}.");

	public void Register(FakeOutputDevice device)
	{
		if (_Devices.ContainsKey(device.DeviceId))
		{
			throw new InvalidOperationException(
				$"A fake output device with id {device.DeviceId} is already registered.");
		}

		_Devices[device.DeviceId] = device;
	}

	OutputDevice IOutputDeviceFactory.Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers) => Get(deviceId);

	public void Dispose()
	{
		_Devices.Dispose();
	}
}
