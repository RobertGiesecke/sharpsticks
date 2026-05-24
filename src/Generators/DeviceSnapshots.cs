using System.Collections.Immutable;
using System.Runtime.InteropServices;
using SharpSticks.DirectInput;
using SharpSticks.InputAbstractions;
using SharpSticks.LinuxInput;
using SharpSticks.VJoy;

namespace SharpSticks.Generators;

internal static class DeviceSnapshots
{
	private static readonly Lazy<(bool Success, ImmutableArray<InputDeviceSnapshot> Devices, string? Error)>
		DirectInputResult = new(EnumerateDirectInputDevicesCore, isThreadSafe: true);

	private static readonly Lazy<(bool Success, ImmutableArray<OutputDeviceSnapshot> Devices, string? Error)>
		OutputDevicesResult = new(EnumerateOutputDevicesCore, isThreadSafe: true);

	public static bool TryEnumerateDirectInputDevices(
		out ImmutableArray<InputDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = DirectInputResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	public static bool TryEnumerateOutputDevices(
		out ImmutableArray<OutputDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = OutputDevicesResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	private static (bool, ImmutableArray<InputDeviceSnapshot>, string?) EnumerateDirectInputDevicesCore()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return EnumerateLinuxInputDevicesCore();
		}

		try
		{
			var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
			var result = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);
			var builder = ImmutableArray.CreateBuilder<InputDeviceSnapshot>();
			foreach (var device in result)
			{
				DeviceCapabilityHelper.TryGetCapabilities(
					directInput, device.InstanceGuid, out var axes, out var buttonCount);
				builder.Add(new(device.DeviceId, device.ProductName, device.ProductGuid, axes, buttonCount));
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			return (false, ImmutableArray<InputDeviceSnapshot>.Empty, GetMessage(exception));
		}
	}

	private static (bool, ImmutableArray<InputDeviceSnapshot>, string?) EnumerateLinuxInputDevicesCore()
	{
		try
		{
			var infos = LinuxInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
			var builder = ImmutableArray.CreateBuilder<InputDeviceSnapshot>(infos.Length);
			foreach (var info in infos)
			{
				// Reuse the existing DirectInputDeviceSnapshot record shape — same field
				// meanings on every platform. Linux button count = number of button codes
				// the kernel reported as supported for that device.
				builder.Add(new(
					info.DeviceId,
					info.ProductName,
					info.ProductGuid,
					info.Axes,
					(uint)info.ButtonCodes.Length));
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			return (false, ImmutableArray<InputDeviceSnapshot>.Empty, GetMessage(exception));
		}
	}

	private static (bool, ImmutableArray<OutputDeviceSnapshot>, string?) EnumerateOutputDevicesCore()
	{
		// uinput-on-Linux outputs only exist at runtime once the user's program creates
		// them — nothing exists at design time, so the generator has no output slots to
		// enumerate. Skipping with success=true keeps OutputDeviceIds empty rather than
		// emitting a diagnostic about "vJoy not enabled".
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return (true, ImmutableArray<OutputDeviceSnapshot>.Empty, null);
		}

		try
		{
			VJoyNative.EnsureLoaded();
			if (!VJoyNative.VJoyEnabled())
			{
				return (false, ImmutableArray<OutputDeviceSnapshot>.Empty, "vJoy is not enabled.");
			}

			var builder = ImmutableArray.CreateBuilder<OutputDeviceSnapshot>();
			// Every vJoy device on Windows surfaces under the same DirectInput ProductGuid
			// (VID 0x1234 / PID 0xBEAD encoded as PIDVID).
			var vJoyInputProductGuid = ProductGuidEncoder.Encode(vendor: 0x1234, product: 0xBEAD);
			for (var deviceId = 1u; deviceId <= VJoyDevices.MaxDeviceId; deviceId++)
			{
				var status = VJoyNative.GetVJDStatus(deviceId);
				if (status is not VjdStatus.Missing and not VjdStatus.Unknown)
				{
					var axes = EnumerateVJoyAxes(deviceId);
					var buttonCount = (uint)Math.Max(0, VJoyNative.GetVJDButtonNumber(deviceId));
					builder.Add(new(deviceId, axes, buttonCount, vJoyInputProductGuid));
				}
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception exception) when (IsExpectedEnumerationFailure(exception))
		{
			return (false, ImmutableArray<OutputDeviceSnapshot>.Empty, GetMessage(exception));
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

internal readonly record struct InputDeviceSnapshot(
	int DeviceId,
	string ProductName,
	Guid ProductGuid,
	ImmutableArray<Axis> Axes,
	uint ButtonCount);

internal readonly record struct OutputDeviceSnapshot(
	uint DeviceId,
	ImmutableArray<Axis> Axes,
	uint ButtonCount,
	Guid InputProductGuid);
