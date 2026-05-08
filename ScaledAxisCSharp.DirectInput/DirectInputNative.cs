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

	public static readonly Guid IidIDirectInput8W =
		new(0xBF798031, 0x483A, 0x4DA2, 0xAA, 0x99, 0x5D, 0x64, 0xED, 0x36, 0x97, 0x00);

	public static readonly Guid GuidXAxis = new(0xA36D02E0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidYAxis = new(0xA36D02E1, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidZAxis = new(0xA36D02E2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidRxAxis = new(0xA36D02F4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidRyAxis = new(0xA36D02F5, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidRzAxis = new(0xA36D02E3, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidSlider = new(0xA36D02E4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidButton = new(0xA36D02F0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidPov = new(0xA36D02F2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

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

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern nint CreateEventW(nint lpEventAttributes, bool bManualReset, bool bInitialState, nint lpName);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool CloseHandle(nint hObject);

	[DllImport("kernel32.dll")]
	public static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	public static extern IntPtr GetDesktopWindow();

	public static bool Succeeded(int hresult)
	{
		return hresult >= 0;
	}

	public static int GetInstance(uint type)
	{
		return (int)((type & 0x00FFFF00) >> 8);
	}

	public static uint GetAxisOffset(PhysicalAxis axis)
	{
		return axis switch
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
	}

	public static uint GetButtonOffset(int zeroBasedButton)
	{
		return 48u + (uint)zeroBasedButton;
	}

	public static uint GetPovOffset(int zeroBasedPov)
	{
		return 32u + ((uint)zeroBasedPov * 4u);
	}

	public static uint GetStateSize()
	{
		return 272;
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
			Size = (uint)sizeof(DirectInputDeviceCaps),
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

	public static int SetEventNotification(nint device, nint eventHandle)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		return vtable->SetEventNotification(device, eventHandle);
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