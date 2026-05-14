using System.Collections.Immutable;
using ScaledAxisCSharp.DirectInput;
using ScaledAxisCSharp.VJoy;

namespace ScaledAxisCSharp.Generators;

internal static class DeviceSnapshots
{
	public static bool TryEnumerateDirectInputDevices(
		out ImmutableArray<DirectInputDeviceSnapshot> devices,
		out string? error)
	{
		try
		{
			var result = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
			var builder = ImmutableArray.CreateBuilder<DirectInputDeviceSnapshot>();
			foreach (var device in result)
			{
				builder.Add(new DirectInputDeviceSnapshot(device.DeviceId, device.ProductName));
			}

			devices = builder.ToImmutable();
			error = null;
			return true;
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			devices = ImmutableArray<DirectInputDeviceSnapshot>.Empty;
			error = GetMessage(exception);
			return false;
		}
	}

	public static bool TryEnumerateOutputDevices(
		out ImmutableArray<VJoyDeviceSnapshot> devices,
		out string? error)
	{
		try
		{
			if (!VJoyDevices.TryEnumerateAvailable(out var result, out error))
			{
				devices = ImmutableArray<VJoyDeviceSnapshot>.Empty;
				return false;
			}

			var builder = ImmutableArray.CreateBuilder<VJoyDeviceSnapshot>();
			foreach (var device in result)
			{
				builder.Add(new VJoyDeviceSnapshot(device.DeviceId));
			}

			devices = builder.ToImmutable();
			error = null;
			return true;
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			devices = ImmutableArray<VJoyDeviceSnapshot>.Empty;
			error = GetMessage(exception);
			return false;
		}
	}

	private static bool IsExpectedEnumerationFailure(Exception exception)
	{
		return exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
			or FileNotFoundException or FileLoadException or InvalidOperationException;
	}

	private static string GetMessage(Exception exception)
	{
		return exception.Message;
	}
}

internal readonly record struct DirectInputDeviceSnapshot(int DeviceId, string ProductName);

internal readonly record struct VJoyDeviceSnapshot(uint DeviceId);
