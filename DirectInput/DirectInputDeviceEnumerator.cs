using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpSticks.DirectInput;

internal static unsafe class DirectInputDeviceEnumerator
{
	private static nint _DirectInput;

	public static ImmutableArray<DirectInputDeviceInfo> EnumerateConnectedDeviceInfos()
	{
		return EnumerateConnectedDeviceInfos(GetOrCreateContext());
	}

	internal static nint GetOrCreateContext()
	{
		if (_DirectInput != 0)
		{
			return _DirectInput;
		}

		var instanceHandle = DirectInputNative.GetModuleHandle(null);
		var result = DirectInputNative.DirectInput8Create(
			instanceHandle,
			DirectInputNative.DirectInputVersion,
			in DirectInputNative.IidIDirectInput8W,
			out var directInputPointer,
			IntPtr.Zero);

		if (!DirectInputNative.Succeeded(result) || directInputPointer == IntPtr.Zero)
		{
			throw new InvalidOperationException($"DirectInput8Create failed with HRESULT 0x{result:X8}.");
		}

		_DirectInput = directInputPointer;
		return _DirectInput;
	}

	internal static ImmutableArray<DirectInputDeviceInfo> EnumerateConnectedDeviceInfos(nint directInput)
	{
		var deviceInfos = ImmutableArray.CreateBuilder<DirectInputDeviceInfo>();
		var handle = GCHandle.Alloc(deviceInfos);

		try
		{
			var enumResult = DirectInputNative.EnumDevices(
				directInput,
				&EnumDevicesCallback,
				GCHandle.ToIntPtr(handle),
				DirectInputNative.DiEdFlAttachedOnly);

			if (!DirectInputNative.Succeeded(enumResult))
			{
				throw new InvalidOperationException($"DirectInput enumeration failed with HRESULT 0x{enumResult:X8}.");
			}

			return deviceInfos.ToImmutable();
		}
		finally
		{
			handle.Free();
		}
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static int EnumDevicesCallback(DirectInputDeviceInstanceNative* instance, nint referenceData)
	{
		var devices = (ImmutableArray<DirectInputDeviceInfo>.Builder)GCHandle.FromIntPtr(referenceData).Target!;
		devices.Add(DirectInputDeviceInfo.FromNative(devices.Count, instance));
		return DirectInputNative.DiEnumContinue;
	}
}
