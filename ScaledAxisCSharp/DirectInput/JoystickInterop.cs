using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ScaledAxisCSharp.Config;

namespace ScaledAxisCSharp.DirectInput;

internal sealed unsafe class JoystickDevice
{
	private const int AxisRangeMin = -32767;
	private const int AxisRangeMax = 32767;
	private readonly nint _devicePointer;

	private JoystickDevice(int deviceId, DirectInputDeviceInfo info, DirectInputDeviceCaps caps, nint devicePointer)
	{
		DeviceId = deviceId;
		InstanceGuid = info.InstanceGuid;
		Name = info.ProductName;
		InstanceName = info.InstanceName;
		Caps = new JoystickCaps(caps.Axes, caps.Buttons, caps.Povs);
		_devicePointer = devicePointer;
	}

	public int DeviceId { get; }
	public Guid InstanceGuid { get; }
	public string Name { get; }
	public string InstanceName { get; }
	public JoystickCaps Caps { get; }

	public bool TryRead(out JoystickState state, out string? error)
	{
		var pollResult = DirectInputNative.Poll(_devicePointer);
		if (!DirectInputNative.Succeeded(pollResult))
		{
			var acquireResult = DirectInputNative.Acquire(_devicePointer);
			if (!DirectInputNative.Succeeded(acquireResult))
			{
				state = default;
				error = $"DirectInput acquire failed for joystick {DeviceId} with HRESULT 0x{acquireResult:X8}.";
				return false;
			}

			pollResult = DirectInputNative.Poll(_devicePointer);
		}

		var stateResult = DirectInputNative.GetDeviceState(_devicePointer, out var rawState);
		if (!DirectInputNative.Succeeded(stateResult))
		{
			var acquireResult = DirectInputNative.Acquire(_devicePointer);
			if (DirectInputNative.Succeeded(acquireResult))
			{
				stateResult = DirectInputNative.GetDeviceState(_devicePointer, out rawState);
			}

			if (!DirectInputNative.Succeeded(stateResult))
			{
				state = default;
				error = $"DirectInput GetDeviceState failed for joystick {DeviceId} with HRESULT 0x{stateResult:X8}.";
				return false;
			}
		}

		state = JoystickState.FromNative(rawState);
		error = null;
		return true;
	}

	public double ReadNormalizedAxis(in JoystickState state, AxisBinding binding)
	{
		var rawValue = state.GetAxisValue(binding.Axis);
		return Normalize(rawValue, AxisRangeMin, AxisRangeMax, binding.Mode, binding.Invert, binding.Deadzone);
	}

	public static IReadOnlyList<JoystickDevice> EnumerateConnected()
	{
		var directInput = DirectInputContext.GetOrCreate();
		var deviceInfos = new List<DirectInputDeviceInfo>();
		var handle = GCHandle.Alloc(deviceInfos);

		try
		{
			var enumResult = DirectInputNative.EnumDevices(
				directInput,
				&EnumDevicesCallback,
				GCHandle.ToIntPtr(handle),
				DirectInputNative.DiEdFlAttachedOnly);

			if (!DirectInputNative.Succeeded(enumResult))
			{
				throw new InvalidOperationException($"DirectInput enumeration failed with HRESULT 0x{enumResult:X8}.");
			}
		}
		finally
		{
			handle.Free();
		}

		var devices = new List<JoystickDevice>();
		for (var index = 0; index < deviceInfos.Count; index++)
		{
			var device = OpenDevice(directInput, deviceInfos[index], index);
			if (device is not null)
			{
				devices.Add(device);
			}
		}

		return devices;
	}

	private static JoystickDevice? OpenDevice(nint directInput, DirectInputDeviceInfo info, int deviceId)
	{
		var instanceGuid = info.InstanceGuid;
		var createResult = DirectInputNative.CreateDevice(directInput, in instanceGuid, out var devicePointer);
		if (!DirectInputNative.Succeeded(createResult) || devicePointer == 0)
		{
			return null;
		}

		var windowHandle = DirectInputNative.GetConsoleWindow();
		if (windowHandle == IntPtr.Zero)
		{
			windowHandle = DirectInputNative.GetDesktopWindow();
		}

		try
		{
			var cooperativeResult = DirectInputNative.SetCooperativeLevel(
				devicePointer,
				windowHandle,
				DirectInputNative.DiSclBackground | DirectInputNative.DiSclNonExclusive);
			if (!DirectInputNative.Succeeded(cooperativeResult))
			{
				return null;
			}

			var objectInfos = EnumerateObjects(devicePointer);
			using var dataFormatScope = DataFormatScope.Create(BuildObjectFormats(objectInfos));
			var dataFormat = dataFormatScope.DataFormat;

			var setFormatResult = DirectInputNative.SetDataFormat(devicePointer, in dataFormat);
			if (!DirectInputNative.Succeeded(setFormatResult))
			{
				return null;
			}

			ConfigureAxisRanges(devicePointer, objectInfos);

			var capsResult = DirectInputNative.GetCapabilities(devicePointer, out var caps);
			if (!DirectInputNative.Succeeded(capsResult))
			{
				return null;
			}

			var acquireResult = DirectInputNative.Acquire(devicePointer);
			if (!DirectInputNative.Succeeded(acquireResult))
			{
				return null;
			}

			return new JoystickDevice(deviceId, info, caps, devicePointer);
		}
		catch
		{
			DirectInputNative.Release(devicePointer);
			throw;
		}
	}

	private static List<DirectInputDeviceObjectInfo> EnumerateObjects(nint devicePointer)
	{
		var objectInfos = new List<DirectInputDeviceObjectInfo>();
		var handle = GCHandle.Alloc(objectInfos);

		try
		{
			var enumResult = DirectInputNative.EnumObjects(devicePointer, &EnumObjectsCallback, GCHandle.ToIntPtr(handle), 0);
			if (!DirectInputNative.Succeeded(enumResult))
			{
				throw new InvalidOperationException($"DirectInput object enumeration failed with HRESULT 0x{enumResult:X8}.");
			}
		}
		finally
		{
			handle.Free();
		}

		return objectInfos;
	}

	private static List<DirectInputObjectDataFormat> BuildObjectFormats(IReadOnlyList<DirectInputDeviceObjectInfo> objects)
	{
		var objectFormats = new List<DirectInputObjectDataFormat>();
		var sliderIndex = 0;

		foreach (var axis in objects.Where(IsAxisObject).OrderBy(GetAxisSortKey))
		{
			var offset = GetAxisOffset(axis.TypeGuid, ref sliderIndex);
			if (offset is null)
			{
				continue;
			}

			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = 0,
				Offset = offset.Value,
				Type = axis.Type,
				Flags = 0,
			});
		}

		foreach (var pov in objects.Where(objectInfo => objectInfo.TypeGuid == DirectInputNative.GuidPov)
			         .OrderBy(objectInfo => DirectInputNative.GetInstance(objectInfo.Type))
			         .Take(4))
		{
			var index = DirectInputNative.GetInstance(pov.Type);
			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = 0,
				Offset = DirectInputNative.GetPovOffset(index),
				Type = pov.Type,
				Flags = 0,
			});
		}

		foreach (var button in objects.Where(objectInfo => objectInfo.TypeGuid == DirectInputNative.GuidButton)
			         .OrderBy(objectInfo => DirectInputNative.GetInstance(objectInfo.Type))
			         .Take(128))
		{
			var index = DirectInputNative.GetInstance(button.Type);
			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = 0,
				Offset = DirectInputNative.GetButtonOffset(index),
				Type = button.Type,
				Flags = 0,
			});
		}

		return objectFormats;
	}

	private static void ConfigureAxisRanges(nint devicePointer, IReadOnlyList<DirectInputDeviceObjectInfo> objects)
	{
		foreach (var axis in objects.Where(IsAxisObject))
		{
			DirectInputNative.SetRangeProperty(devicePointer, axis.Offset, AxisRangeMin, AxisRangeMax);
		}
	}

	private static bool IsAxisObject(DirectInputDeviceObjectInfo objectInfo)
	{
		return objectInfo.TypeGuid == DirectInputNative.GuidXAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidYAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidZAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidRxAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidRyAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidRzAxis ||
		       objectInfo.TypeGuid == DirectInputNative.GuidSlider;
	}

	private static int GetAxisSortKey(DirectInputDeviceObjectInfo objectInfo)
	{
		if (objectInfo.TypeGuid == DirectInputNative.GuidXAxis) return 0;
		if (objectInfo.TypeGuid == DirectInputNative.GuidYAxis) return 1;
		if (objectInfo.TypeGuid == DirectInputNative.GuidZAxis) return 2;
		if (objectInfo.TypeGuid == DirectInputNative.GuidRxAxis) return 3;
		if (objectInfo.TypeGuid == DirectInputNative.GuidRyAxis) return 4;
		if (objectInfo.TypeGuid == DirectInputNative.GuidRzAxis) return 5;
		if (objectInfo.TypeGuid == DirectInputNative.GuidSlider) return 6 + DirectInputNative.GetInstance(objectInfo.Type);
		return int.MaxValue;
	}

	private static uint? GetAxisOffset(Guid axisGuid, ref int sliderIndex)
	{
		if (axisGuid == DirectInputNative.GuidXAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.X);
		if (axisGuid == DirectInputNative.GuidYAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.Y);
		if (axisGuid == DirectInputNative.GuidZAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.Z);
		if (axisGuid == DirectInputNative.GuidRxAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.Rx);
		if (axisGuid == DirectInputNative.GuidRyAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.Ry);
		if (axisGuid == DirectInputNative.GuidRzAxis) return DirectInputNative.GetAxisOffset(PhysicalAxis.Rz);
		if (axisGuid == DirectInputNative.GuidSlider && sliderIndex < 2)
		{
			var offset = DirectInputNative.GetAxisOffset(sliderIndex == 0 ? PhysicalAxis.Slider1 : PhysicalAxis.Slider2);
			sliderIndex++;
			return offset;
		}

		return null;
	}

	private static double Normalize(int rawValue, int min, int max, AxisMode mode, bool invert, double deadzone)
	{
		if (max <= min)
		{
			return 0.0;
		}

		var normalized = (rawValue - min) / (double)(max - min);
		normalized = Math.Clamp(normalized, 0.0, 1.0);

		if (mode == AxisMode.Signed)
		{
			normalized = (normalized * 2.0) - 1.0;
		}

		if (invert)
		{
			normalized = mode == AxisMode.Signed ? -normalized : 1.0 - normalized;
		}

		deadzone = Math.Clamp(deadzone, 0.0, 0.99);
		if (deadzone > 0.0)
		{
			normalized = mode == AxisMode.Signed
				? ApplySignedDeadzone(normalized, deadzone)
				: ApplyUnsignedDeadzone(normalized, deadzone);
		}

		return normalized;
	}

	private static double ApplySignedDeadzone(double value, double deadzone)
	{
		var magnitude = Math.Abs(value);
		if (magnitude <= deadzone)
		{
			return 0.0;
		}

		var adjusted = (magnitude - deadzone) / (1.0 - deadzone);
		return Math.CopySign(adjusted, value);
	}

	private static double ApplyUnsignedDeadzone(double value, double deadzone)
	{
		if (value <= deadzone)
		{
			return 0.0;
		}

		return (value - deadzone) / (1.0 - deadzone);
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static int EnumDevicesCallback(DirectInputDeviceInstanceNative* instance, nint referenceData)
	{
		var devices = (List<DirectInputDeviceInfo>)GCHandle.FromIntPtr(referenceData).Target!;
		devices.Add(DirectInputDeviceInfo.FromNative(instance));
		return DirectInputNative.DiEnumContinue;
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static int EnumObjectsCallback(DirectInputDeviceObjectInstanceNative* instance, nint referenceData)
	{
		var objects = (List<DirectInputDeviceObjectInfo>)GCHandle.FromIntPtr(referenceData).Target!;
		objects.Add(DirectInputDeviceObjectInfo.FromNative(instance));
		return DirectInputNative.DiEnumContinue;
	}

	private sealed class DataFormatScope : IDisposable
	{
		private readonly nint _objectBuffer;

		private DataFormatScope(nint objectBuffer, DirectInputDataFormat dataFormat)
		{
			_objectBuffer = objectBuffer;
			DataFormat = dataFormat;
		}

		public DirectInputDataFormat DataFormat { get; }

		public static DataFormatScope Create(IReadOnlyList<DirectInputObjectDataFormat> objectFormats)
		{
			var objectSize = Marshal.SizeOf<DirectInputObjectDataFormat>();
			var buffer = Marshal.AllocHGlobal(objectFormats.Count * objectSize);

			for (var index = 0; index < objectFormats.Count; index++)
			{
				var target = buffer + (index * objectSize);
				Marshal.StructureToPtr(objectFormats[index], target, false);
			}

			var dataFormat = new DirectInputDataFormat
			{
				Size = (uint)Marshal.SizeOf<DirectInputDataFormat>(),
				ObjectSize = (uint)objectSize,
				Flags = DirectInputNative.DiDfAbsAxis,
				DataSize = DirectInputNative.GetStateSize(),
				ObjectCount = (uint)objectFormats.Count,
				ObjectDataFormats = buffer,
			};

			return new DataFormatScope(buffer, dataFormat);
		}

		public void Dispose()
		{
			Marshal.FreeHGlobal(_objectBuffer);
		}
	}

	private static class DirectInputContext
	{
		private static nint _directInput;

		public static nint GetOrCreate()
		{
			if (_directInput != 0)
			{
				return _directInput;
			}

			var instanceHandle = DirectInputNative.GetModuleHandle(null);
			var result = DirectInputNative.DirectInput8Create(
				instanceHandle,
				DirectInputNative.DirectInputVersion,
				in DirectInputNative.IID_IDirectInput8W,
				out var directInputPointer,
				IntPtr.Zero);

			if (!DirectInputNative.Succeeded(result) || directInputPointer == IntPtr.Zero)
			{
				throw new InvalidOperationException($"DirectInput8Create failed with HRESULT 0x{result:X8}.");
			}

			_directInput = directInputPointer;
			return _directInput;
		}
	}
}

internal readonly record struct JoystickCaps(uint NumAxes, uint NumButtons, uint NumPovs);

internal readonly record struct JoystickState(
	int X,
	int Y,
	int Z,
	int Rx,
	int Ry,
	int Rz,
	int Slider1,
	int Slider2,
	ulong ButtonBitsLow,
	ulong ButtonBitsHigh)
{
	public static unsafe JoystickState FromNative(DirectInputJoyState2 state)
	{
		ulong buttonBitsLow = 0;
		ulong buttonBitsHigh = 0;

		for (var index = 0; index < 64; index++)
		{
			if ((state.Buttons[index] & 0x80) != 0)
			{
				buttonBitsLow |= 1UL << index;
			}
		}

		for (var index = 0; index < 64; index++)
		{
			if ((state.Buttons[index + 64] & 0x80) != 0)
			{
				buttonBitsHigh |= 1UL << index;
			}
		}

		return new JoystickState(
			state.X,
			state.Y,
			state.Z,
			state.Rx,
			state.Ry,
			state.Rz,
			state.Sliders[0],
			state.Sliders[1],
			buttonBitsLow,
			buttonBitsHigh);
	}

	public int GetAxisValue(PhysicalAxis axis)
	{
		return axis switch
		{
			PhysicalAxis.X => X,
			PhysicalAxis.Y => Y,
			PhysicalAxis.Z => Z,
			PhysicalAxis.Rx => Rx,
			PhysicalAxis.Ry => Ry,
			PhysicalAxis.Rz => Rz,
			PhysicalAxis.Slider1 => Slider1,
			PhysicalAxis.Slider2 => Slider2,
			_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
		};
	}

	public bool IsButtonPressed(int buttonNumber)
	{
		if (buttonNumber < 1 || buttonNumber > 128)
		{
			return false;
		}

		var zeroBasedIndex = buttonNumber - 1;
		if (zeroBasedIndex < 64)
		{
			return ((ButtonBitsLow >> zeroBasedIndex) & 1UL) != 0;
		}

		return ((ButtonBitsHigh >> (zeroBasedIndex - 64)) & 1UL) != 0;
	}
}

internal readonly record struct DirectInputDeviceInfo(Guid InstanceGuid, string ProductName, string InstanceName)
{
	public static unsafe DirectInputDeviceInfo FromNative(DirectInputDeviceInstanceNative* native)
	{
		return new DirectInputDeviceInfo(
			native->InstanceGuid,
			ReadNullTerminatedString(native->ProductName, 260),
			ReadNullTerminatedString(native->InstanceName, 260));
	}

	private static unsafe string ReadNullTerminatedString(char* buffer, int capacity)
	{
		var length = 0;
		while (length < capacity && buffer[length] != '\0')
		{
			length++;
		}

		return new string(buffer, 0, length);
	}
}

internal readonly record struct DirectInputDeviceObjectInfo(Guid TypeGuid, uint Offset, uint Type)
{
	public static unsafe DirectInputDeviceObjectInfo FromNative(DirectInputDeviceObjectInstanceNative* native)
	{
		return new DirectInputDeviceObjectInfo(native->TypeGuid, native->Offset, native->Type);
	}
}
