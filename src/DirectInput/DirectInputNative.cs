using System.Runtime.InteropServices;

namespace SharpSticks.DirectInput;

internal static unsafe partial class DirectInputNative
{
	public const uint DiDfAbsAxis = 0x00000001;
	public const uint DiPhByOffset = 1;
	public const uint DiSclNonExclusive = 0x00000002;
	public const uint DiSclBackground = 0x00000008;

	public static readonly Guid GuidButton = new(0xA36D02F0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid GuidPov = new(0xA36D02F2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00,
		0x00);

	public static readonly Guid DiPropRange = new(4, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	public static partial nint CreateEventW(nint lpEventAttributes, [MarshalAs(UnmanagedType.Bool)] bool bManualReset, [MarshalAs(UnmanagedType.Bool)] bool bInitialState, nint lpName);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool CloseHandle(nint hObject);

	[LibraryImport("kernel32.dll")]
	public static partial IntPtr GetConsoleWindow();

	[LibraryImport("user32.dll")]
	public static partial IntPtr GetDesktopWindow();

	public static int GetInstance(uint type)
	{
		return (int)((type & 0x00FFFF00) >> 8);
	}

	public static uint GetAxisOffset(Axis axis)
	{
		return axis switch
		{
			Axis.X => 0,
			Axis.Y => 4,
			Axis.Z => 8,
			Axis.Rx => 12,
			Axis.Ry => 16,
			Axis.Rz => 20,
			Axis.Slider1 => 24,
			Axis.Slider2 => 28,
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

	public static int CreateDevice(nint directInput, in Guid instanceGuid, out nint devicePointer)
	{
		var vtable = *(DirectInput8VTable**)directInput;
		fixed (Guid* guidPointer = &instanceGuid)
		fixed (nint* devicePointerTarget = &devicePointer)
		{
			return vtable->CreateDevice(directInput, guidPointer, devicePointerTarget, 0);
		}
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
			Header = new()
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
		range = new()
		{
			Header = new()
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
		caps = new()
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

	public static int Unacquire(nint device)
	{
		var vtable = *(DirectInputDevice8VTable**)device;
		var unacquire = (delegate* unmanaged[Stdcall]<nint, int>)vtable->Unacquire;
		return unacquire(device);
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
