using System.Runtime.InteropServices;

namespace ScaledAxisCSharp.DirectInput;

internal static unsafe partial class DirectInputNative
{
	public const uint DirectInputVersion = 0x0800;
	public const uint Di8DevClassGameCtrl = 4;
	public const uint DiEdFlAttachedOnly = 0x00000001;
	public const int DiEnumContinue = 1;

	public static readonly Guid IidIDirectInput8W =
		new(0xBF798031, 0x483A, 0x4DA2, 0xAA, 0x99, 0x5D, 0x64, 0xED, 0x36, 0x97, 0x00);

	[DllImport("dinput8.dll", PreserveSig = true)]
	public static extern int DirectInput8Create(
		IntPtr hinst,
		uint dwVersion,
		in Guid riidltf,
		out IntPtr ppvOut,
		IntPtr punkOuter);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr GetModuleHandle(string? lpModuleName);

	public static bool Succeeded(int hresult)
	{
		return hresult >= 0;
	}

	public static int Release(nint comObject)
	{
		if (comObject == 0)
		{
			return 0;
		}

		var vtable = *(UnknownVTable**)comObject;
		return (int)vtable->Release(comObject);
	}

	public static int EnumDevices(
		nint directInput,
		delegate* unmanaged[Stdcall]<DirectInputDeviceInstanceNative*, nint, int> callback,
		nint referenceData,
		uint flags)
	{
		var vtable = *(DirectInput8VTable**)directInput;
		return vtable->EnumDevices(directInput, Di8DevClassGameCtrl, callback, referenceData, flags);
	}
}
