using System.Runtime.InteropServices;

namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnknownVTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
}
