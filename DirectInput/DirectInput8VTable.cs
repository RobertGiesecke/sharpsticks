using System.Runtime.InteropServices;

namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DirectInput8VTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, nint, int> CreateDevice;

	public delegate* unmanaged[Stdcall]<nint, uint, delegate* unmanaged[Stdcall]<DirectInputDeviceInstanceNative*, nint,
		int>, nint, uint, int> EnumDevices;
}
