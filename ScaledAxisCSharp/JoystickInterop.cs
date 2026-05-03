using System.Runtime.InteropServices;

namespace ScaledAxisCSharp;

internal sealed class JoystickDevice
{
	private const int AxisRangeMin = -32767;
	private const int AxisRangeMax = 32767;
	private readonly IDirectInputDevice8W _device;

	private JoystickDevice(int deviceId, DirectInputDeviceInstanceW instance, DirectInputDeviceCaps caps, IDirectInputDevice8W device)
	{
		DeviceId = deviceId;
		InstanceGuid = instance.InstanceGuid;
		Name = instance.ProductName;
		InstanceName = instance.InstanceName;
		Caps = new JoystickCaps(caps.Axes, caps.Buttons, caps.Povs);
		_device = device;
	}

	public int DeviceId { get; }
	public Guid InstanceGuid { get; }
	public string Name { get; }
	public string InstanceName { get; }
	public JoystickCaps Caps { get; }

	public bool TryRead(out JoystickState state, out string? error)
	{
		var rawState = DirectInputJoyState2.CreateEmpty();

		var pollResult = _device.Poll();
		if (!DirectInputNative.Succeeded(pollResult))
		{
			var acquireResult = _device.Acquire();
			if (!DirectInputNative.Succeeded(acquireResult))
			{
				state = default;
				error = $"DirectInput acquire failed for joystick {DeviceId} with HRESULT 0x{acquireResult:X8}.";
				return false;
			}

			pollResult = _device.Poll();
		}

		var stateResult = _device.GetDeviceState(Marshal.SizeOf<DirectInputJoyState2>(), ref rawState);
		if (!DirectInputNative.Succeeded(stateResult))
		{
			var acquireResult = _device.Acquire();
			if (DirectInputNative.Succeeded(acquireResult))
			{
				stateResult = _device.GetDeviceState(Marshal.SizeOf<DirectInputJoyState2>(), ref rawState);
			}

			if (!DirectInputNative.Succeeded(stateResult))
			{
				state = default;
				error = $"DirectInput GetDeviceState failed for joystick {DeviceId} with HRESULT 0x{stateResult:X8}.";
				return false;
			}
		}

		state = new JoystickState(rawState);
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
		var instances = new List<DirectInputDeviceInstanceW>();
		var callback = new DirectInputEnumDevicesCallback((ref DirectInputDeviceInstanceW instance, IntPtr _) =>
		{
			instances.Add(instance);
			return DirectInputNative.DiEnumContinue;
		});
		var callbackPointer = Marshal.GetFunctionPointerForDelegate(callback);

		var enumResult = directInput.EnumDevices(
			DirectInputNative.Di8DevClassAll,
			callbackPointer,
			IntPtr.Zero,
			DirectInputNative.DiEdFlAttachedOnly);

		if (!DirectInputNative.Succeeded(enumResult))
		{
			throw new InvalidOperationException($"DirectInput enumeration failed with HRESULT 0x{enumResult:X8}.");
		}

		var devices = new List<JoystickDevice>();
		for (var index = 0; index < instances.Count; index++)
		{
			var device = OpenDevice(directInput, instances[index], index);
			if (device is not null)
			{
				devices.Add(device);
			}
		}

		return devices;
	}

	private static JoystickDevice? OpenDevice(IDirectInput8W directInput, DirectInputDeviceInstanceW instance, int deviceId)
	{
		var createResult = directInput.CreateDevice(instance.InstanceGuid, out var device, IntPtr.Zero);
		if (!DirectInputNative.Succeeded(createResult))
		{
			return null;
		}

		var windowHandle = DirectInputNative.GetConsoleWindow();
		if (windowHandle == IntPtr.Zero)
		{
			windowHandle = DirectInputNative.GetDesktopWindow();
		}

		var cooperativeResult = device.SetCooperativeLevel(
			windowHandle,
			DirectInputNative.DiSclBackground | DirectInputNative.DiSclNonExclusive);
		if (!DirectInputNative.Succeeded(cooperativeResult))
		{
			return null;
		}

		var objects = EnumerateObjects(device);
		using var dataFormatScope = DataFormatScope.Create(BuildObjectFormats(objects));
		var dataFormat = dataFormatScope.DataFormat;

		var setFormatResult = device.SetDataFormat(ref dataFormat);
		if (!DirectInputNative.Succeeded(setFormatResult))
		{
			return null;
		}

		ConfigureAxisRanges(device, objects);

		var caps = new DirectInputDeviceCaps
		{
			Size = (uint)Marshal.SizeOf<DirectInputDeviceCaps>()
		};

		var capsResult = device.GetCapabilities(ref caps);
		if (!DirectInputNative.Succeeded(capsResult))
		{
			return null;
		}

		var acquireResult = device.Acquire();
		if (!DirectInputNative.Succeeded(acquireResult))
		{
			return null;
		}

		return new JoystickDevice(deviceId, instance, caps, device);
	}

	private static List<DirectInputDeviceObjectInstanceW> EnumerateObjects(IDirectInputDevice8W device)
	{
		var objects = new List<DirectInputDeviceObjectInstanceW>();
		var callback = new DirectInputEnumDeviceObjectsCallback((ref DirectInputDeviceObjectInstanceW instance, IntPtr _) =>
		{
			objects.Add(instance);
			return DirectInputNative.DiEnumContinue;
		});
		var callbackPointer = Marshal.GetFunctionPointerForDelegate(callback);

		var enumResult = device.EnumObjects(callbackPointer, IntPtr.Zero, 0);
		if (!DirectInputNative.Succeeded(enumResult))
		{
			throw new InvalidOperationException($"DirectInput object enumeration failed with HRESULT 0x{enumResult:X8}.");
		}

		return objects;
	}

	private static List<DirectInputObjectDataFormat> BuildObjectFormats(IReadOnlyList<DirectInputDeviceObjectInstanceW> objects)
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
				GuidPointer = IntPtr.Zero,
				Offset = (uint)offset.Value,
				Type = axis.Type,
				Flags = 0,
			});
		}

		foreach (var pov in objects.Where(objectInstance => objectInstance.TypeGuid == DirectInputNative.GuidPov)
			         .OrderBy(objectInstance => DirectInputNative.GetInstance(objectInstance.Type))
			         .Take(4))
		{
			var index = DirectInputNative.GetInstance(pov.Type);
			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = IntPtr.Zero,
				Offset = (uint)(Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Povs)).ToInt32() + (index * sizeof(uint))),
				Type = pov.Type,
				Flags = 0,
			});
		}

		foreach (var button in objects.Where(objectInstance => objectInstance.TypeGuid == DirectInputNative.GuidButton)
			         .OrderBy(objectInstance => DirectInputNative.GetInstance(objectInstance.Type))
			         .Take(128))
		{
			var index = DirectInputNative.GetInstance(button.Type);
			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = IntPtr.Zero,
				Offset = (uint)(Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Buttons)).ToInt32() + index),
				Type = button.Type,
				Flags = 0,
			});
		}

		return objectFormats;
	}

	private static void ConfigureAxisRanges(IDirectInputDevice8W device, IReadOnlyList<DirectInputDeviceObjectInstanceW> objects)
	{
		foreach (var axis in objects.Where(IsAxisObject))
		{
			var range = new DirectInputPropertyRange
			{
				Header = new DirectInputPropertyHeader
				{
					Size = (uint)Marshal.SizeOf<DirectInputPropertyRange>(),
					HeaderSize = (uint)Marshal.SizeOf<DirectInputPropertyHeader>(),
					Object = axis.Offset,
					How = DirectInputNative.DiPhByOffset,
				},
				Min = AxisRangeMin,
				Max = AxisRangeMax,
			};

			device.SetProperty(in DirectInputNative.DiPropRange, ref range);
		}
	}

	private static bool IsAxisObject(DirectInputDeviceObjectInstanceW objectInstance)
	{
		return objectInstance.TypeGuid == DirectInputNative.GuidXAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidYAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidZAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidRxAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidRyAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidRzAxis ||
		       objectInstance.TypeGuid == DirectInputNative.GuidSlider;
	}

	private static int GetAxisSortKey(DirectInputDeviceObjectInstanceW objectInstance)
	{
		if (objectInstance.TypeGuid == DirectInputNative.GuidXAxis) return 0;
		if (objectInstance.TypeGuid == DirectInputNative.GuidYAxis) return 1;
		if (objectInstance.TypeGuid == DirectInputNative.GuidZAxis) return 2;
		if (objectInstance.TypeGuid == DirectInputNative.GuidRxAxis) return 3;
		if (objectInstance.TypeGuid == DirectInputNative.GuidRyAxis) return 4;
		if (objectInstance.TypeGuid == DirectInputNative.GuidRzAxis) return 5;
		if (objectInstance.TypeGuid == DirectInputNative.GuidSlider) return 6 + DirectInputNative.GetInstance(objectInstance.Type);
		return int.MaxValue;
	}

	private static int? GetAxisOffset(Guid axisGuid, ref int sliderIndex)
	{
		if (axisGuid == DirectInputNative.GuidXAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.X)).ToInt32();
		if (axisGuid == DirectInputNative.GuidYAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Y)).ToInt32();
		if (axisGuid == DirectInputNative.GuidZAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Z)).ToInt32();
		if (axisGuid == DirectInputNative.GuidRxAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Rx)).ToInt32();
		if (axisGuid == DirectInputNative.GuidRyAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Ry)).ToInt32();
		if (axisGuid == DirectInputNative.GuidRzAxis) return Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Rz)).ToInt32();
		if (axisGuid == DirectInputNative.GuidSlider && sliderIndex < 2)
		{
			var offset = Marshal.OffsetOf<DirectInputJoyState2>(nameof(DirectInputJoyState2.Sliders)).ToInt32() + (sliderIndex * sizeof(int));
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

	private sealed class DataFormatScope : IDisposable
	{
		private readonly IntPtr _objectBuffer;

		private DataFormatScope(IntPtr objectBuffer, DirectInputDataFormat dataFormat)
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
				DataSize = (uint)Marshal.SizeOf<DirectInputJoyState2>(),
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
		private static IDirectInput8W? _directInput;

		public static IDirectInput8W GetOrCreate()
		{
			if (_directInput is not null)
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

			try
			{
				_directInput = (IDirectInput8W)Marshal.GetObjectForIUnknown(directInputPointer);
				return _directInput;
			}
			finally
			{
				Marshal.Release(directInputPointer);
			}
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
	public JoystickState(DirectInputJoyState2 state)
		: this(
			state.X,
			state.Y,
			state.Z,
			state.Rx,
			state.Ry,
			state.Rz,
			state.Sliders[0],
			state.Sliders[1],
			PackButtons(state.Buttons, 0),
			PackButtons(state.Buttons, 64))
	{
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

	private static ulong PackButtons(IReadOnlyList<byte> buttons, int startIndex)
	{
		ulong bits = 0;

		for (var bit = 0; bit < 64 && startIndex + bit < buttons.Count; bit++)
		{
			if ((buttons[startIndex + bit] & 0x80) != 0)
			{
				bits |= 1UL << bit;
			}
		}

		return bits;
	}
}
