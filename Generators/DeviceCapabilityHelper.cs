using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Generators;

using DirectInput;

internal static unsafe class DeviceCapabilityHelper
{
	private static readonly Guid GuidXAxis =
		new(0xA36D02E0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidYAxis =
		new(0xA36D02E1, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidZAxis =
		new(0xA36D02E2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidRxAxis =
		new(0xA36D02F4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidRyAxis =
		new(0xA36D02F5, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidRzAxis =
		new(0xA36D02E3, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	private static readonly Guid GuidSlider =
		new(0xA36D02E4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00);

	internal static bool TryGetCapabilities(
		nint directInput,
		Guid instanceGuid,
		out ImmutableArray<Axis> axes,
		out uint buttonCount)
	{
		if (!TryCreateDevice(directInput, instanceGuid, out var devicePointer))
		{
			axes = ImmutableArray<Axis>.Empty;
			buttonCount = 0;
			return false;
		}

		try
		{
			axes = EnumerateAxes(devicePointer);
			buttonCount = GetButtonCount(devicePointer);
			return true;
		}
		finally
		{
			DirectInputNative.Release(devicePointer);
		}
	}

	private static bool TryCreateDevice(nint directInput, Guid instanceGuid, out nint devicePointer)
	{
		var vtable = *(DirectInput8VTable**)directInput;
		nint device = 0;
		var result = vtable->CreateDevice(directInput, &instanceGuid, &device, 0);
		devicePointer = device;
		return DirectInputNative.Succeeded(result) && device != 0;
	}

	private static uint GetButtonCount(nint devicePointer)
	{
		var vtable = *(DeviceCapVTable**)devicePointer;
		var caps = new DeviceCaps { Size = (uint)sizeof(DeviceCaps) };
		var result = vtable->GetCapabilities(devicePointer, &caps);
		return DirectInputNative.Succeeded(result) ? caps.Buttons : 0;
	}

	private static ImmutableArray<Axis> EnumerateAxes(nint devicePointer)
	{
		var objectInfos = new List<(Guid TypeGuid, uint Type)>();
		var handle = GCHandle.Alloc(objectInfos);

		try
		{
			var vtable = *(DeviceCapVTable**)devicePointer;
			vtable->EnumObjects(devicePointer, &EnumObjectsCallback, GCHandle.ToIntPtr(handle), 0);
		}
		finally
		{
			handle.Free();
		}

		var builder = ImmutableArray.CreateBuilder<Axis>();
		var sliderCount = 0;

		foreach (var obj in objectInfos
			         .Where(o => IsAxisGuid(o.TypeGuid))
			         .OrderBy(o => GetAxisSortKey(o.TypeGuid, o.Type)))
		{
			var name = Axis.GetDirectInputAxis(obj.TypeGuid, ref sliderCount);
			if (name is not null)
			{
				builder.Add(name.Value);
			}
		}

		return builder.ToImmutable();
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static int EnumObjectsCallback(DeviceObjectInstanceNative* instance, nint referenceData)
	{
		var list = (List<(Guid, uint)>)GCHandle.FromIntPtr(referenceData).Target!;
		list.Add((instance->TypeGuid, instance->Type));
		return 1; // DIENUM_CONTINUE
	}

	private static bool IsAxisGuid(Guid guid) =>
		guid == GuidXAxis || guid == GuidYAxis || guid == GuidZAxis ||
		guid == GuidRxAxis || guid == GuidRyAxis || guid == GuidRzAxis ||
		guid == GuidSlider;

	private static int GetAxisSortKey(Guid guid, uint type) =>
		guid switch
		{
			_ when guid == GuidXAxis => 0,
			_ when guid == GuidYAxis => 1,
			_ when guid == GuidZAxis => 2,
			_ when guid == GuidRxAxis => 3,
			_ when guid == GuidRyAxis => 4,
			_ when guid == GuidRzAxis => 5,
			_ when guid == GuidSlider => 6 + (int)((type & 0x00FFFF00) >> 8),
			_ => int.MaxValue,
		};

	[StructLayout(LayoutKind.Sequential)]
	private struct DeviceCapVTable
	{
		public nint QueryInterface;
		public nint AddRef;
		public nint Release;
		public delegate* unmanaged[Stdcall]<nint, DeviceCaps*, int> GetCapabilities;

		public delegate* unmanaged[Stdcall]<nint,
			delegate* unmanaged[Stdcall]<DeviceObjectInstanceNative*, nint, int>, nint, uint, int> EnumObjects;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct DeviceCaps
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

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct DeviceObjectInstanceNative
	{
		public uint Size;
		public Guid TypeGuid;
		public uint Offset;
		public uint Type;
	}
}