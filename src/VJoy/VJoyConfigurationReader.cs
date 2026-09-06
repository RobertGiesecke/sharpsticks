using System.Collections.Immutable;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace SharpSticks.VJoy;

/// Design-time enumeration of the configured vJoy slots. Reads the HID report descriptor
/// vJoyConf stores per slot under the driver's service key and never goes through
/// vJoyInterface.dll: the interface DLL opens a handle to the device for its capability
/// queries and keeps it for the life of the process, which every other feeder sees as
/// Busy. Inside Rider or the compiler server that is a permanent lock on the device.
/// Counterpart of DirectInputCapabilityReader on the input side.
internal static class VJoyConfigurationReader
{
	private const string ParametersKeyPath = @"SYSTEM\CurrentControlSet\Services\vjoy\Parameters";

	/// vJoy 2.1.x stored the descriptor under a misspelled name. The 2.2.x forks (the ones
	/// with extended axes) fixed the spelling, and their driver reads the fixed name; a
	/// machine can carry both with different content, so the fixed name wins.
	private const string DescriptorValueName = "HidReportDescriptor";

	private const string LegacyDescriptorValueName = "HidReportDesctiptor";

	private const string SizeSuffix = "Size";

	public static ImmutableArray<AvailableOutputDevice> EnumerateConfiguredDevices()
	{
		if (!OperatingSystem.IsWindows())
		{
			return ImmutableArray<AvailableOutputDevice>.Empty;
		}

		try
		{
			return EnumerateConfiguredDevicesCore();
		}
		catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
		{
			return ImmutableArray<AvailableOutputDevice>.Empty;
		}
	}

	[SupportedOSPlatform("windows")]
	private static ImmutableArray<AvailableOutputDevice> EnumerateConfiguredDevicesCore()
	{
		using var parameters = Registry.LocalMachine.OpenSubKey(ParametersKeyPath, writable: false);
		if (parameters is null)
		{
			return ImmutableArray<AvailableOutputDevice>.Empty;
		}

		var builder = ImmutableArray.CreateBuilder<AvailableOutputDevice>();
		for (var deviceId = 1u; deviceId <= VJoyDevices.MaxDeviceId; deviceId++)
		{
			using var device = parameters.OpenSubKey($"Device{deviceId:00}", writable: false);
			if (device is null || !TryReadDescriptor(device, out var descriptor))
			{
				continue;
			}

			if (!VJoyHidReportDescriptor.TryParse(descriptor, out var axes, out var buttonCount))
			{
				continue;
			}

			builder.Add(new(
				deviceId, axes, buttonCount, VJoyDeviceFactory.VJoyProductGuid, VJoyDeviceFactory.VJoyProductName));
		}

		return builder.ToImmutable();
	}

	[SupportedOSPlatform("windows")]
	private static bool TryReadDescriptor(RegistryKey device, out ReadOnlySpan<byte> descriptor) =>
		TryReadDescriptor(device, DescriptorValueName, out descriptor)
		|| TryReadDescriptor(device, LegacyDescriptorValueName, out descriptor);

	[SupportedOSPlatform("windows")]
	private static bool TryReadDescriptor(RegistryKey device, string valueName, out ReadOnlySpan<byte> descriptor)
	{
		if (device.GetValue(valueName) is not byte[] { Length: > 0 } bytes)
		{
			descriptor = default;
			return false;
		}

		// The blob may be padded; the companion DWORD carries the descriptor's real length.
		descriptor = device.GetValue(valueName + SizeSuffix) is int size && size > 0 && size < bytes.Length
			? bytes.AsSpan(0, size)
			: bytes;
		return true;
	}
}
