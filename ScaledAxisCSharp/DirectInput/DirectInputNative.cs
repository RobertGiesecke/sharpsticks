namespace ScaledAxisCSharp.DirectInput;

internal static unsafe class DirectInputNative
{
	public const uint DirectInputVersion = 0x0800;
	public const uint Di8DevClassGameCtrl = 4;
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

	public static uint GetAxisOffset(PhysicalAxis axis) => axis switch
	{
		PhysicalAxis.X => 0,
		PhysicalAxis.Y => 4,
		PhysicalAxis.Z => 8,
		PhysicalAxis.Rx => 12,
		PhysicalAxis.Ry => 16,
		PhysicalAxis.Rz => 20,
		PhysicalAxis.Slider1 => 24,
		PhysicalAxis.Slider2 => 28,
		_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
	};

	public static uint GetButtonOffset(int zeroBasedButton) => 48u + (uint)zeroBasedButton;

	public static uint GetPovOffset(int zeroBasedPov) => 32u + ((uint)zeroBasedPov * 4u);

	public static uint GetStateSize() => 272;

	public static int Release(nint comObject)
	{
		if (comObject == 0)
		{
			return 0;
		}

		var vtable = *(IUnknownVTable**)comObject;
		return (int)vtable->Release(comObject);
	}

	public static int CreateDevice(nint directInput, in Guid instanceGuid, out nint devicePointer)
	{
		var vtable = *(DirectInput8VTable**)directInput;
		fixed (Guid* guidPointer = &instanceGuid)
		fixed (nint* devicePointerTarget = &devicePointer)
		{
			return vtable->CreateDevice(directInput, guidPointer, devicePointerTarget, 0);
		}
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

	public static int SetCooperativeLevel(nint device, nint windowHandle, uint flags)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		return vtable->SetCooperativeLevel(device, windowHandle, flags);
	}

	public static int SetDataFormat(nint device, in DirectInputDataFormat dataFormat)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		fixed (DirectInputDataFormat* dataFormatPointer = &dataFormat)
		{
			return vtable->SetDataFormat(device, dataFormatPointer);
		}
	}

	public static int EnumObjects(
		nint device,
		delegate* unmanaged[Stdcall]<DirectInputDeviceObjectInstanceNative*, nint, int> callback,
		nint referenceData,
		uint flags)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		return vtable->EnumObjects(device, callback, referenceData, flags);
	}

	public static int SetRangeProperty(nint device, uint offset, int min, int max)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		var range = new DirectInputPropertyRange
		{
			Header = new DirectInputPropertyHeader
			{
				Size = (uint)sizeof(DirectInputPropertyRange),
				HeaderSize = (uint)sizeof(DirectInputPropertyHeader),
				Object = offset,
				How = DiPhByOffset,
			},
			Min = min,
			Max = max,
		};

		var propertyGuid = DiPropRange;
		return vtable->SetProperty(device, &propertyGuid, &range);
	}

	public static int GetRangeProperty(nint device, uint offset, out DirectInputPropertyRange range)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		range = new DirectInputPropertyRange
		{
			Header = new DirectInputPropertyHeader
			{
				Size = (uint)sizeof(DirectInputPropertyRange),
				HeaderSize = (uint)sizeof(DirectInputPropertyHeader),
				Object = offset,
				How = DiPhByOffset,
			},
		};

		var propertyGuid = DiPropRange;
		fixed (DirectInputPropertyRange* rangePointer = &range)
		{
			return vtable->GetProperty(device, &propertyGuid, rangePointer);
		}
	}

	public static int GetCapabilities(nint device, out DirectInputDeviceCaps caps)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		caps = new DirectInputDeviceCaps
		{
			Size = (uint)sizeof(DirectInputDeviceCaps)
		};

		fixed (DirectInputDeviceCaps* capsPointer = &caps)
		{
			return vtable->GetCapabilities(device, capsPointer);
		}
	}

	public static int Acquire(nint device)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		return vtable->Acquire(device);
	}

	public static int Poll(nint device)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		return vtable->Poll(device);
	}

	public static int GetDeviceState(nint device, out DirectInputJoyState2 state)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		state = default;

		fixed (DirectInputJoyState2* statePointer = &state)
		{
			return vtable->GetDeviceState(device, (int)GetStateSize(), statePointer);
		}
	}
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IUnknownVTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DirectInput8VTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, nint, int> CreateDevice;
	public delegate* unmanaged[Stdcall]<nint, uint, delegate* unmanaged[Stdcall]<DirectInputDeviceInstanceNative*, nint, int>, nint, uint, int> EnumDevices;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DirectInputDevice8VTable
{
	public delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> QueryInterface;
	public delegate* unmanaged[Stdcall]<nint, uint> AddRef;
	public delegate* unmanaged[Stdcall]<nint, uint> Release;
	public delegate* unmanaged[Stdcall]<nint, DirectInputDeviceCaps*, int> GetCapabilities;
	public delegate* unmanaged[Stdcall]<nint, delegate* unmanaged[Stdcall]<DirectInputDeviceObjectInstanceNative*, nint, int>, nint, uint, int> EnumObjects;
	public delegate* unmanaged[Stdcall]<nint, Guid*, DirectInputPropertyRange*, int> GetProperty;
	public delegate* unmanaged[Stdcall]<nint, Guid*, DirectInputPropertyRange*, int> SetProperty;
	public delegate* unmanaged[Stdcall]<nint, int> Acquire;
	public nint Unacquire;
	public delegate* unmanaged[Stdcall]<nint, int, DirectInputJoyState2*, int> GetDeviceState;
	public nint GetDeviceData;
	public delegate* unmanaged[Stdcall]<nint, DirectInputDataFormat*, int> SetDataFormat;
	public nint SetEventNotification;
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

[StructLayout(LayoutKind.Sequential)]
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
internal unsafe struct DirectInputDeviceInstanceNative
{
	public uint Size;
	public Guid InstanceGuid;
	public Guid ProductGuid;
	public uint DeviceType;
	public fixed char InstanceName[260];
	public fixed char ProductName[260];
	public Guid ForceFeedbackDriverGuid;
	public ushort UsagePage;
	public ushort Usage;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DirectInputDeviceObjectInstanceNative
{
	public uint Size;
	public Guid TypeGuid;
	public uint Offset;
	public uint Type;
	public uint Flags;
	public fixed char Name[260];
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
	public nint GuidPointer;
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
	public nint ObjectDataFormats;
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
internal unsafe struct DirectInputJoyState2
{
	public int X;
	public int Y;
	public int Z;
	public int Rx;
	public int Ry;
	public int Rz;
	public fixed int Sliders[2];
	public fixed uint Povs[4];
	public fixed byte Buttons[128];
	public fixed int ExtendedAxes[24];
}
