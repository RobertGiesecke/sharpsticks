namespace SharpSticks.DirectInput;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DirectInputDevice8VTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
	public delegate* unmanaged[Stdcall]<nint, DirectInputDeviceCaps*, int> GetCapabilities;

	public delegate* unmanaged[Stdcall]<nint, delegate* unmanaged[Stdcall]<DirectInputDeviceObjectInstanceNative*, nint,
		int>, nint, uint, int> EnumObjects;

	public delegate* unmanaged[Stdcall]<nint, Guid*, DirectInputPropertyRange*, int> GetProperty;
	public delegate* unmanaged[Stdcall]<nint, Guid*, DirectInputPropertyRange*, int> SetProperty;
	public delegate* unmanaged[Stdcall]<nint, int> Acquire;
	public nint Unacquire;
	public delegate* unmanaged[Stdcall]<nint, int, DirectInputJoyState2*, int> GetDeviceState;
	public nint GetDeviceData;
	public delegate* unmanaged[Stdcall]<nint, DirectInputDataFormat*, int> SetDataFormat;
	public delegate* unmanaged[Stdcall]<nint, nint, int> SetEventNotification;
	public delegate* unmanaged[Stdcall]<nint, nint, uint, int> SetCooperativeLevel;
	public nint GetObjectInfo;
	public nint GetDeviceInfo;
	public nint RunControlPanel;
	public nint Initialize;
	public nint CreateEffect;
	public nint EnumEffects;
	public nint GetEffectInfo;
	public nint GetForceFeedbackState;
	public nint SendForceFeedbackCommand;
	public nint EnumCreatedEffectObjects;
	public nint Escape;
	public delegate* unmanaged[Stdcall]<nint, int> Poll;
}