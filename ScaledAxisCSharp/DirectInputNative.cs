using System.Runtime.InteropServices;

namespace ScaledAxisCSharp;

internal static class DirectInputNative
{
	public const uint DirectInputVersion = 0x0800;
	public const uint Di8DevClassAll = 0;
	public const uint DiEdFlAttachedOnly = 0x00000001;
	public const uint DiDfAbsAxis = 0x00000001;
	public const uint DiPhByOffset = 1;
	public const uint DiSclNonExclusive = 0x00000002;
	public const uint DiSclBackground = 0x00000008;
	public const int DiEnumContinue = 1;

	public static readonly Guid IID_IDirectInput8W = new(0xBF798031, 0x483A, 0x4DA2, 0xAA, 0x99, 0x5D, 0x64, 0xED, 0x36, 0x97, 0x00);
	public static readonly Guid GuidXAxis = new(0xA36D02E0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidYAxis = new(0xA36D02E1, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidZAxis = new(0xA36D02E2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidRxAxis = new(0xA36D02F4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidRyAxis = new(0xA36D02F5, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidRzAxis = new(0xA36D02E3, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidSlider = new(0xA36D02E4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidButton = new(0xA36D02F0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid GuidPov = new(0xA36D02F2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);
	public static readonly Guid DiPropRange = new(4, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46);

	[DllImport("dinput8.dll", PreserveSig = true)]
	public static extern int DirectInput8Create(
		IntPtr hinst,
		uint dwVersion,
		in Guid riidltf,
		out IntPtr ppvOut,
		IntPtr punkOuter);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr GetModuleHandle(string? lpModuleName);

	[DllImport("kernel32.dll")]
	public static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	public static extern IntPtr GetDesktopWindow();

	public static bool Succeeded(int hresult) => hresult >= 0;

	public static int GetInstance(uint type) => (int)((type & 0x00FFFF00) >> 8);
}

[ComImport]
[Guid("BF798031-483A-4DA2-AA99-5D64ED369700")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirectInput8W
{
	void QueryInterface(in Guid riid, out IntPtr ppvObject);
	uint AddRef();
	uint Release();
	int CreateDevice(in Guid rguid, [MarshalAs(UnmanagedType.Interface)] out IDirectInputDevice8W directInputDevice, IntPtr unknownOuter);
	int EnumDevices(uint devType, IntPtr callback, IntPtr referenceData, uint flags);
	int GetDeviceStatus(in Guid rguidInstance);
	int RunControlPanel(IntPtr hwndOwner, uint flags);
	int Initialize(IntPtr hinstance, uint version);
	int FindDevice(in Guid rguidClass, [MarshalAs(UnmanagedType.LPWStr)] string name, out Guid instanceGuid);
	int EnumDevicesBySemantics(IntPtr userName, IntPtr actionFormat, IntPtr callback, IntPtr referenceData, uint flags);
	int ConfigureDevices(IntPtr callback, IntPtr parameters, uint flags, IntPtr referenceData);
}

[ComImport]
[Guid("54D41081-DC15-4833-A41B-748F73A38179")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirectInputDevice8W
{
	void QueryInterface(in Guid riid, out IntPtr ppvObject);
	uint AddRef();
	uint Release();
	int GetCapabilities(ref DirectInputDeviceCaps caps);
	int EnumObjects(IntPtr callback, IntPtr referenceData, uint flags);
	int GetProperty(in Guid propertyGuid, ref DirectInputPropertyHeader header);
	int SetProperty(in Guid propertyGuid, ref DirectInputPropertyRange range);
	int Acquire();
	int Unacquire();
	int GetDeviceState(int cbData, ref DirectInputJoyState2 data);
	int GetDeviceData(int cbObjectData, IntPtr objectData, ref uint entries, uint flags);
	int SetDataFormat(ref DirectInputDataFormat dataFormat);
	int SetEventNotification(IntPtr eventHandle);
	int SetCooperativeLevel(IntPtr hwnd, uint flags);
	int GetObjectInfo(ref DirectInputDeviceObjectInstanceW objectInstance, uint objectId, uint how);
	int GetDeviceInfo(ref DirectInputDeviceInstanceW instance);
	int RunControlPanel(IntPtr hwndOwner, uint flags);
	int Initialize(IntPtr hinstance, uint version, in Guid rguid);
	int CreateEffect(IntPtr rguid, IntPtr effect, IntPtr directInputEffect, IntPtr unknownOuter);
	int EnumEffects(IntPtr callback, IntPtr referenceData, uint effectType);
	int GetEffectInfo(IntPtr effectInfo, IntPtr rguid);
	int GetForceFeedbackState(out uint outState);
	int SendForceFeedbackCommand(uint flags);
	int EnumCreatedEffectObjects(IntPtr callback, IntPtr referenceData, uint flags);
	int Escape(IntPtr escape);
	int Poll();
	int SendDeviceData(uint cbObjectData, IntPtr objectData, ref uint entries, uint flags);
	int EnumEffectsInFile([MarshalAs(UnmanagedType.LPWStr)] string fileName, IntPtr callback, IntPtr referenceData, uint flags);
	int WriteEffectToFile([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint entries, IntPtr fileEffects, uint flags);
	int BuildActionMap(IntPtr actionFormat, [MarshalAs(UnmanagedType.LPWStr)] string userName, uint flags);
	int SetActionMap(IntPtr actionFormat, [MarshalAs(UnmanagedType.LPWStr)] string userName, uint flags);
	int GetImageInfo(IntPtr deviceImageInfoHeader);
}

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int DirectInputEnumDevicesCallback(ref DirectInputDeviceInstanceW instance, IntPtr referenceData);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int DirectInputEnumDeviceObjectsCallback(ref DirectInputDeviceObjectInstanceW objectInstance, IntPtr referenceData);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DirectInputDeviceCaps
{
	public uint Size;
	public uint Flags;
	public uint DeviceType;
	public uint Axes;
	public uint Buttons;
	public uint Povs;
	public uint ForceFeedbackSamplePeriod;
	public uint ForceFeedbackMinTimeResolution;
	public uint FirmwareRevision;
	public uint HardwareRevision;
	public uint ForceFeedbackDriverVersion;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DirectInputDeviceInstanceW
{
	public uint Size;
	public Guid InstanceGuid;
	public Guid ProductGuid;
	public uint DeviceType;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
	public string InstanceName;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
	public string ProductName;

	public Guid ForceFeedbackDriverGuid;
	public ushort UsagePage;
	public ushort Usage;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DirectInputDeviceObjectInstanceW
{
	public uint Size;
	public Guid TypeGuid;
	public uint Offset;
	public uint Type;
	public uint Flags;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
	public string Name;

	public uint ForceFeedbackMaxForce;
	public uint ForceFeedbackForceResolution;
	public ushort CollectionNumber;
	public ushort DesignatorIndex;
	public ushort UsagePage;
	public ushort Usage;
	public uint Dimension;
	public ushort Exponent;
	public ushort ReportId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputObjectDataFormat
{
	public IntPtr GuidPointer;
	public uint Offset;
	public uint Type;
	public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputDataFormat
{
	public uint Size;
	public uint ObjectSize;
	public uint Flags;
	public uint DataSize;
	public uint ObjectCount;
	public IntPtr ObjectDataFormats;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputPropertyHeader
{
	public uint Size;
	public uint HeaderSize;
	public uint Object;
	public uint How;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputPropertyRange
{
	public DirectInputPropertyHeader Header;
	public int Min;
	public int Max;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DirectInputJoyState2
{
	public int X;
	public int Y;
	public int Z;
	public int Rx;
	public int Ry;
	public int Rz;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
	public int[] Sliders;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	public uint[] Povs;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
	public byte[] Buttons;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
	public int[] ExtendedAxes;

	public static DirectInputJoyState2 CreateEmpty()
	{
		return new DirectInputJoyState2
		{
			Sliders = new int[2],
			Povs = new uint[4],
			Buttons = new byte[128],
			ExtendedAxes = new int[24],
		};
	}
}
