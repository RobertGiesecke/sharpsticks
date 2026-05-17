using System.Collections.Immutable;
using SharpSticks.DirectInput;
using SharpSticks.InputAbstractions;
using SharpSticks.VJoy;

namespace SharpSticks.Generators;

internal static class DeviceSnapshots
{
	private static readonly Lazy<(bool Success, ImmutableArray<DirectInputDeviceSnapshot> Devices, string? Error)>
		DirectInputResult = new(EnumerateDirectInputDevicesCore, isThreadSafe: true);

	private static readonly Lazy<(bool Success, ImmutableArray<VJoyDeviceSnapshot> Devices, string? Error)>
		OutputDevicesResult = new(EnumerateOutputDevicesCore, isThreadSafe: true);

	public static bool TryEnumerateDirectInputDevices(
		out ImmutableArray<DirectInputDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = DirectInputResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	public static bool TryEnumerateOutputDevices(
		out ImmutableArray<VJoyDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = OutputDevicesResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	private static (bool, ImmutableArray<DirectInputDeviceSnapshot>, string?) EnumerateDirectInputDevicesCore()
	{
		try
		{
			var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
			var result = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);
			var builder = ImmutableArray.CreateBuilder<DirectInputDeviceSnapshot>();
			foreach (var device in result)
			{
				DeviceCapabilityHelper.TryGetCapabilities(
					directInput, device.InstanceGuid, out var axes, out var buttonCount);
				builder.Add(new(device.DeviceId, device.ProductName, axes, buttonCount));
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			return (false, ImmutableArray<DirectInputDeviceSnapshot>.Empty, GetMessage(exception));
		}
	}

	private static (bool, ImmutableArray<VJoyDeviceSnapshot>, string?) EnumerateOutputDevicesCore()
	{
		try
		{
			VJoyNative.EnsureLoaded();
			if (!VJoyNative.VJoyEnabled())
			{
				return (false, ImmutableArray<VJoyDeviceSnapshot>.Empty, "vJoy is not enabled.");
			}

			var builder = ImmutableArray.CreateBuilder<VJoyDeviceSnapshot>();
			for (var deviceId = 1u; deviceId <= VJoyDevices.MaxDeviceId; deviceId++)
			{
				var status = VJoyNative.GetVJDStatus(deviceId);
				if (status is not VjdStatus.Missing and not VjdStatus.Unknown)
				{
					var axes = EnumerateVJoyAxes(deviceId);
					var buttonCount = (uint)Math.Max(0, VJoyNative.GetVJDButtonNumber(deviceId));
					builder.Add(new(deviceId, axes, buttonCount));
				}
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			return (false, ImmutableArray<VJoyDeviceSnapshot>.Empty, GetMessage(exception));
		}
	}

	private static ImmutableArray<Axis> EnumerateVJoyAxes(uint deviceId)
	{
		var builder = ImmutableArray.CreateBuilder<Axis>();
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x30))
		{
			builder.Add(Axis.X);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x31))
		{
			builder.Add(Axis.Y);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x32))
		{
			builder.Add(Axis.Z);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x33))
		{
			builder.Add(Axis.Rx);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x34))
		{
			builder.Add(Axis.Ry);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x35))
		{
			builder.Add(Axis.Rz);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x36))
		{
			builder.Add(Axis.Slider1);
		}

		if (VJoyNative.GetVJDAxisExist(deviceId, 0x37))
		{
			builder.Add(Axis.Slider2);
		}

		return builder.ToImmutable();
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

internal readonly record struct DirectInputDeviceSnapshot(
	int DeviceId,
	string ProductName,
	ImmutableArray<Axis> Axes,
	uint ButtonCount);

internal readonly record struct VJoyDeviceSnapshot(uint DeviceId, ImmutableArray<Axis> Axes, uint ButtonCount);
