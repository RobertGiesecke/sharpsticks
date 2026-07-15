namespace SharpSticks.OutputAbstractions;

/// A compile-time [OutputDevice] declaration, carried to runtime so a device is materialized
/// with its full declared capability rather than just the buttons/axes that happen to be routed.
public readonly record struct DeclaredOutputDevice(uint DeviceId, uint ButtonCount, ImmutableArray<Axis> Axes);

/// Registry of [OutputDevice] declarations. The source generator emits a module initializer that
/// registers each declaration here as the consumer assembly loads; the runtime then merges these
/// with the routed capabilities when it creates a device (see RuntimeBuilder). Registration all
/// happens at module-init time (before any device is built), but the lock keeps it safe against a
/// caller that registers late.
public static class DeclaredOutputDevices
{
	private static readonly Dictionary<uint, DeclaredOutputDevice> ByDeviceId = new();
	private static readonly Lock Gate = new();

	public static void Register(uint deviceId, uint buttonCount, ImmutableArray<Axis> axes)
	{
		lock (Gate)
		{
			ByDeviceId[deviceId] = new(deviceId, buttonCount, axes);
		}
	}

	public static bool TryGet(uint deviceId, out DeclaredOutputDevice device)
	{
		lock (Gate)
		{
			return ByDeviceId.TryGetValue(deviceId, out device);
		}
	}
}
