using System.Collections.Immutable;

namespace ScaledAxisCSharp.VJoy;

public static class VJoyDevices
{
	public const uint MaxDeviceId = 16;

	public static bool TryEnumerateAvailable(
		out ImmutableArray<VJoyDeviceInfo> devices,
		out string? error,
		uint maxDeviceId = MaxDeviceId)
	{
		devices = ImmutableArray<VJoyDeviceInfo>.Empty;

		try
		{
			VJoyNative.EnsureLoaded();
			if (!VJoyNative.VJoyEnabled())
			{
				error = "vJoy is not enabled.";
				return false;
			}

			var builder = ImmutableArray.CreateBuilder<VJoyDeviceInfo>();
			for (var deviceId = 1u; deviceId <= maxDeviceId; deviceId++)
			{
				var status = VJoyNative.GetVJDStatus(deviceId);
				if (status is VjdStatus.Free or VjdStatus.Own)
				{
					builder.Add(new VJoyDeviceInfo(deviceId, status));
				}
			}

			devices = builder.ToImmutable();
			error = null;
			return true;
		}
		catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException
			                                  or BadImageFormatException)
		{
			error = exception.Message;
			return false;
		}
	}
}
