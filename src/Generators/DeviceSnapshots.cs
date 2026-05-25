using System.Collections.Immutable;
using SharpSticks.InputAbstractions;
using SharpSticks.OutputAbstractions;
using SharpSticks.PlatformDefaults;

namespace SharpSticks.Generators;

internal static class DeviceSnapshots
{
	private static readonly Lazy<(bool Success, ImmutableArray<InputDeviceSnapshot> Devices, string? Error)>
		InputResult = new(EnumerateInputDevicesCore, isThreadSafe: true);

	private static readonly Lazy<(bool Success, ImmutableArray<OutputDeviceSnapshot> Devices, string? Error)>
		OutputResult = new(EnumerateOutputDevicesCore, isThreadSafe: true);

	public static bool TryEnumerateInputDevices(
		out ImmutableArray<InputDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = InputResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	public static bool TryEnumerateOutputDevices(
		out ImmutableArray<OutputDeviceSnapshot> devices,
		out string? error)
	{
		var (success, cachedDevices, cachedError) = OutputResult.Value;
		devices = cachedDevices;
		error = cachedError;
		return success;
	}

	private static (bool, ImmutableArray<InputDeviceSnapshot>, string?) EnumerateInputDevicesCore()
	{
		try
		{
			var available = PlatformDefaultDeviceFactory.Instance.EnumerateAvailableInputs();
			var builder = ImmutableArray.CreateBuilder<InputDeviceSnapshot>(available.Length);
			foreach (var device in available)
			{
				builder.Add(new(device.DeviceId, device.ProductName, device.ProductGuid, device.Axes, device.ButtonCount));
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception ex)
		{
			return (false, ImmutableArray<InputDeviceSnapshot>.Empty, ex.Message);
		}
	}

	private static (bool, ImmutableArray<OutputDeviceSnapshot>, string?) EnumerateOutputDevicesCore()
	{
		try
		{
			var available = PlatformDefaultDeviceFactory.Instance.EnumerateAvailableOutputs();
			var builder = ImmutableArray.CreateBuilder<OutputDeviceSnapshot>(available.Length);
			foreach (var slot in available)
			{
				builder.Add(new(slot.DeviceId, slot.Axes, slot.ButtonCount, slot.InputProductGuid));
			}

			return (true, builder.ToImmutable(), null);
		}
		catch (Exception ex)
		{
			return (false, ImmutableArray<OutputDeviceSnapshot>.Empty, ex.Message);
		}
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
