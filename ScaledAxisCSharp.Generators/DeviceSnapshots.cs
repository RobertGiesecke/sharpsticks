using System.Collections.Immutable;
using ScaledAxisCSharp.DirectInput;
using ScaledAxisCSharp.InputAbstractions;
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
			var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
			var result = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);
			var builder = ImmutableArray.CreateBuilder<DirectInputDeviceSnapshot>();
			foreach (var device in result)
			{
				DeviceCapabilityHelper.TryGetCapabilities(
					directInput, device.InstanceGuid, out var axes, out var buttonCount);
				builder.Add(new DirectInputDeviceSnapshot(device.DeviceId, device.ProductName, axes, buttonCount));
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
			VJoyNative.EnsureLoaded();
			if (!VJoyNative.VJoyEnabled())
			{
				devices = ImmutableArray<VJoyDeviceSnapshot>.Empty;
				error = "vJoy is not enabled.";
				return false;
			}

			var builder = ImmutableArray.CreateBuilder<VJoyDeviceSnapshot>();
			for (var deviceId = 1u; deviceId <= VJoyDevices.MaxDeviceId; deviceId++)
			{
				var status = VJoyNative.GetVJDStatus(deviceId);
				if (status is not VjdStatus.Missing and not VjdStatus.Unknown)
				{
					var axes = EnumerateVJoyAxes(deviceId);
					var buttonCount = (uint)Math.Max(0, VJoyNative.GetVJDButtonNumber(deviceId));
					builder.Add(new VJoyDeviceSnapshot(deviceId, axes, buttonCount));
				}
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
