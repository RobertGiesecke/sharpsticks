using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Generators;

using ScaledAxisCSharp.DirectInput;

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
		out ImmutableArray<PhysicalAxis> axes,
		out uint buttonCount)
	{
		if (!TryCreateDevice(directInput, instanceGuid, out var devicePointer))
		{
			axes = ImmutableArray<PhysicalAxis>.Empty;
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

	private static ImmutableArray<PhysicalAxis> EnumerateAxes(nint devicePointer)
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

		var builder = ImmutableArray.CreateBuilder<PhysicalAxis>();
		var sliderCount = 0;

		foreach (var obj in objectInfos
			         .Where(o => IsAxisGuid(o.TypeGuid))
			         .OrderBy(o => GetAxisSortKey(o.TypeGuid, o.Type)))
		{
			var name =  PhysicalAxis.GetDirectInputPhysicalAxis(obj.TypeGuid, ref sliderCount);
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

	private static int GetAxisSortKey(Guid guid, uint type)
	{
		if (guid == GuidXAxis) return 0;
		if (guid == GuidYAxis) return 1;
		if (guid == GuidZAxis) return 2;
		if (guid == GuidRxAxis) return 3;
		if (guid == GuidRyAxis) return 4;
		if (guid == GuidRzAxis) return 5;
		if (guid == GuidSlider) return 6 + (int)((type & 0x00FFFF00) >> 8);
		return int.MaxValue;
	}

	private static string? GetAxisName(Guid guid, ref int sliderCount)
	{
		if (guid == GuidXAxis) return "X";
		if (guid == GuidYAxis) return "Y";
		if (guid == GuidZAxis) return "Z";
		if (guid == GuidRxAxis) return "Rx";
		if (guid == GuidRyAxis) return "Ry";
		if (guid == GuidRzAxis) return "Rz";
		if (guid == GuidSlider && sliderCount < 2)
		{
			var name = sliderCount == 0 ? "Slider1" : "Slider2";
			sliderCount++;
			return name;
		}

		return null;
	}

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct DeviceCapVTable
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
