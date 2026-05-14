using System.Diagnostics.CodeAnalysis;

namespace ScaledAxisCSharp.DirectInput;

public sealed unsafe class DirectInputJoystickDevice : JoystickDevice
{
	private const int AxisRangeMin = -32767;
	private const int AxisRangeMax = 32767;
	private const int DefaultAxisRangeMin = 0;
	private const int DefaultAxisRangeMax = 65535;
	private readonly Dictionary<PhysicalAxis, AxisDecoderKind> _AxisDecoderKinds = [];
	private readonly IReadOnlyDictionary<PhysicalAxis, AxisRange> _AxisRanges;
	private readonly nint _DevicePointer;
	private bool _Disposed;

	[SetsRequiredMembers]
	private DirectInputJoystickDevice(
		int deviceId,
		DirectInputDeviceInfo info,
		DirectInputDeviceCaps caps,
		nint devicePointer,
		ImmutableArray<PhysicalAxis> physicalAxes,
		IReadOnlyDictionary<PhysicalAxis, AxisRange> axisRanges,
		WaitHandle dataAvailable)
	{
		DeviceId = deviceId;
		InstanceGuid = info.InstanceGuid;
		Name = info.ProductName;
		InstanceName = info.InstanceName;
		Capabilities = new JoystickCapabilities(caps.Axes, caps.Buttons, caps.Povs);
		PhysicalAxes = physicalAxes;
		_DevicePointer = devicePointer;
		_AxisRanges = axisRanges;
		DataAvailable = dataAvailable;
	}

	public Guid InstanceGuid { get; }

	public override void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		DirectInputNative.SetEventNotification(_DevicePointer, 0);
		DataAvailable.Dispose();
		DirectInputNative.Unacquire(_DevicePointer);
		DirectInputNative.Release(_DevicePointer);
	}

	public override bool TryRead(out JoystickState state, out string? error)
	{
		var pollResult = DirectInputNative.Poll(_DevicePointer);
		if (!DirectInputNative.Succeeded(pollResult))
		{
			var acquireResult = DirectInputNative.Acquire(_DevicePointer);
			if (!DirectInputNative.Succeeded(acquireResult))
			{
				state = default;
				error = $"DirectInput acquire failed for joystick {DeviceId} with HRESULT 0x{acquireResult:X8}.";
				return false;
			}

			pollResult = DirectInputNative.Poll(_DevicePointer);
		}

		var stateResult = DirectInputNative.GetDeviceState(_DevicePointer, out var rawState);
		if (!DirectInputNative.Succeeded(stateResult))
		{
			var acquireResult = DirectInputNative.Acquire(_DevicePointer);
			if (DirectInputNative.Succeeded(acquireResult))
			{
				stateResult = DirectInputNative.GetDeviceState(_DevicePointer, out rawState);
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

	public override double ReadNormalizedAxis(in JoystickState state, AxisBinding binding)
	{
		return ReadAxisDebugSample(state, binding).NormalizedValue;
	}

	public override AxisDebugSample ReadAxisDebugSample(in JoystickState state, AxisBinding binding)
	{
		var rawValue = state.GetAxisValue(binding.Axis);
		if (!_AxisRanges.TryGetValue(binding.Axis, out var range))
		{
			range = GetFallbackRange(rawValue);
		}

		var normalized = Normalize(
			binding.Axis,
			rawValue,
			range,
			binding.Mode,
			binding.Invert,
			binding.Deadzone,
			out var decoderKind);
		return new AxisDebugSample(rawValue, range.Min, range.Max, normalized, decoderKind);
	}

	private static AxisRange GetFallbackRange(int rawValue)
	{
		if (rawValue < AxisRangeMin || rawValue > AxisRangeMax)
		{
			return new AxisRange(DefaultAxisRangeMin, DefaultAxisRangeMax);
		}

		return new AxisRange(AxisRangeMin, AxisRangeMax);
	}

	public static PooledList<DirectInputJoystickDevice> EnumerateConnected()
	{
		var directInput = DirectInputDeviceEnumerator.GetOrCreateContext();
		var deviceInfos = DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos(directInput);

		var devices = new PooledList<DirectInputJoystickDevice>(deviceInfos.Length);
		try
		{
			foreach (var deviceInfo in deviceInfos)
			{
				var device = OpenDevice(directInput, deviceInfo);
				if (device is not null)
				{
					devices.Add(device);
				}
			}

			return devices;
		}
		catch
		{
			DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}

	public static ImmutableArray<DirectInputDeviceInfo> EnumerateConnectedDeviceInfos()
	{
		return DirectInputDeviceEnumerator.EnumerateConnectedDeviceInfos();
	}

	private static DirectInputJoystickDevice? OpenDevice(nint directInput, DirectInputDeviceInfo info)
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

		AutoResetEvent? dataAvailable = null;
		var success = false;
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

			using var objectInfos = new PooledList<DirectInputDeviceObjectInfo>();
			EnumerateObjects(devicePointer, objectInfos);
			using var axisEntries = new PooledList<AxisFormatEntry>(objectInfos.Count);
			BuildAxisFormatEntries(objectInfos, axisEntries);

			using var inputObjectDataFormats = new PooledList<DirectInputObjectDataFormat>(axisEntries.Count);
			BuildObjectFormats(axisEntries, objectInfos, inputObjectDataFormats);

			using var dataFormatScope = DataFormatScope.Create(inputObjectDataFormats);
			var dataFormat = dataFormatScope.DataFormat;

			var setFormatResult = DirectInputNative.SetDataFormat(devicePointer, in dataFormat);
			if (!DirectInputNative.Succeeded(setFormatResult))
			{
				return null;
			}

			var eventHandle = DirectInputNative.CreateEventW(0, false, false, 0);
			if (eventHandle == 0)
			{
				return null;
			}

			var setEventResult = DirectInputNative.SetEventNotification(devicePointer, eventHandle);
			if (!DirectInputNative.Succeeded(setEventResult))
			{
				DirectInputNative.CloseHandle(eventHandle);
				return null;
			}

			dataAvailable = new AutoResetEvent(false);
			dataAvailable.SafeWaitHandle = new SafeWaitHandle(eventHandle, ownsHandle: true);

			var physicalAxes = axisEntries.ConvertAll(entry => entry.Axis);
			var axisRanges = ConfigureAxisRanges(devicePointer, axisEntries);

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

			success = true;
			return new DirectInputJoystickDevice(
				info.DeviceId,
				info,
				caps,
				devicePointer,
				[..physicalAxes],
				axisRanges,
				dataAvailable);
		}
		finally
		{
			if (!success)
			{
				DirectInputNative.SetEventNotification(devicePointer, 0);
				dataAvailable?.Dispose();
				DirectInputNative.Unacquire(devicePointer);
				DirectInputNative.Release(devicePointer);
			}
		}
	}

	private static void EnumerateObjects(nint devicePointer, ICollection<DirectInputDeviceObjectInfo> objectInfos)
	{
		var handle = GCHandle.Alloc(objectInfos);

		try
		{
			var enumResult =
				DirectInputNative.EnumObjects(devicePointer, &EnumObjectsCallback, GCHandle.ToIntPtr(handle), 0);
			if (!DirectInputNative.Succeeded(enumResult))
			{
				throw new InvalidOperationException(
					$"DirectInput object enumeration failed with HRESULT 0x{enumResult:X8}.");
			}
		}
		finally
		{
			handle.Free();
		}
	}

	private static void BuildAxisFormatEntries(IReadOnlyList<DirectInputDeviceObjectInfo> objects,
		ICollection<AxisFormatEntry> axisEntries)
	{
		var sliderIndex = 0;

		foreach (var axis in objects.Where(IsAxisObject).OrderBy(GetAxisSortKey))
		{
			var physicalAxis = PhysicalAxis.GetDirectInputPhysicalAxis(axis.TypeGuid, ref sliderIndex);
			if (physicalAxis is null)
			{
				continue;
			}

			axisEntries.Add(new AxisFormatEntry(
				physicalAxis.Value,
				DirectInputNative.GetAxisOffset(physicalAxis.Value),
				axis.Type));
		}
	}

	private static void BuildObjectFormats(
		IReadOnlyList<AxisFormatEntry> axisEntries,
		IReadOnlyList<DirectInputDeviceObjectInfo> objects,
		ICollection<DirectInputObjectDataFormat> objectFormats)
	{
		foreach (var axisEntry in axisEntries)
		{
			objectFormats.Add(new DirectInputObjectDataFormat
			{
				GuidPointer = 0,
				Offset = axisEntry.Offset,
				Type = axisEntry.Type,
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
	}

	private static Dictionary<PhysicalAxis, AxisRange> ConfigureAxisRanges(nint devicePointer,
		IReadOnlyList<AxisFormatEntry> axisEntries)
	{
		var ranges = new Dictionary<PhysicalAxis, AxisRange>();

		foreach (var axisEntry in axisEntries)
		{
			DirectInputNative.SetRangeProperty(devicePointer, axisEntry.Offset, AxisRangeMin, AxisRangeMax);

			if (DirectInputNative.Succeeded(
				    DirectInputNative.GetRangeProperty(devicePointer, axisEntry.Offset, out var range)))
			{
				ranges[axisEntry.Axis] = new AxisRange(range.Min, range.Max);
			}
			else
			{
				ranges[axisEntry.Axis] = new AxisRange(AxisRangeMin, AxisRangeMax);
			}
		}

		return ranges;
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
		return objectInfo.TypeGuid switch
		{
			var guid when guid == DirectInputNative.GuidXAxis => 0,
			var guid when guid == DirectInputNative.GuidYAxis => 1,
			var guid when guid == DirectInputNative.GuidZAxis => 2,
			var guid when guid == DirectInputNative.GuidRxAxis => 3,
			var guid when guid == DirectInputNative.GuidRyAxis => 4,
			var guid when guid == DirectInputNative.GuidRzAxis => 5,
			var guid when guid == DirectInputNative.GuidSlider => 6 + DirectInputNative.GetInstance(objectInfo.Type),
			_ => int.MaxValue,
		};
	}

	private double Normalize(
		PhysicalAxis axis,
		int rawValue,
		AxisRange range,
		AxisMode mode,
		bool invert,
		double deadzone,
		out AxisDecoderKind decoderKind)
	{
		var normalized = NormalizeBase(axis, rawValue, range, mode, out decoderKind);

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

	private double NormalizeBase(
		PhysicalAxis axis,
		int rawValue,
		AxisRange range,
		AxisMode mode,
		out AxisDecoderKind decoderKind)
	{
		if (range.Max <= range.Min)
		{
			decoderKind = AxisDecoderKind.Unknown;
			return 0.0;
		}

		if (mode == AxisMode.Unsigned)
		{
			decoderKind = AxisDecoderKind.Unsigned;
			if (range is { Min: < 0, Max: > 0 })
			{
				// Some non-centering controls still report raw values in 0..65535 even after we set
				// the DirectInput range to a centered signed interval. In unsigned mode we want the
				// full physical travel, so reinterpret that centered range as 0..fullScale.
				return NormalizeUnsigned(rawValue, 0, GetUnsignedCenteredMax(range));
			}

			return NormalizeUnsigned(rawValue, range.Min, range.Max);
		}

		if (range is { Min: < 0, Max: > 0 })
		{
			decoderKind = GetOrDetectSignedDecoder(axis, rawValue, range);
			return decoderKind switch
			{
				AxisDecoderKind.NativeSigned => NormalizeSignedFromNativeRange(rawValue, range),
				AxisDecoderKind.UnsignedCentered => NormalizeSignedFromUnsignedCenteredRange(rawValue, range),
				_ => NormalizeSignedFromUnsignedCenteredRange(rawValue, range),
			};
		}

		decoderKind = AxisDecoderKind.NativeSigned;
		return NormalizeSignedFromNativeRange(rawValue, range);
	}

	private static double NormalizeUnsigned(int rawValue, int min, int max)
	{
		if (max <= min)
		{
			return 0.0;
		}

		var normalized = (rawValue - min) / (double)(max - min);
		return Math.Clamp(normalized, 0.0, 1.0);
	}

	private AxisDecoderKind GetOrDetectSignedDecoder(PhysicalAxis axis, int rawValue, AxisRange range)
	{
		if (_AxisDecoderKinds.TryGetValue(axis, out var existing) &&
		    existing is AxisDecoderKind.NativeSigned or AxisDecoderKind.UnsignedCentered)
		{
			if (existing == AxisDecoderKind.UnsignedCentered && rawValue < 0)
			{
				_AxisDecoderKinds[axis] = AxisDecoderKind.NativeSigned;
				return AxisDecoderKind.NativeSigned;
			}

			if (existing == AxisDecoderKind.NativeSigned && rawValue > range.Max)
			{
				_AxisDecoderKinds[axis] = AxisDecoderKind.UnsignedCentered;
				return AxisDecoderKind.UnsignedCentered;
			}

			return existing;
		}

		var detected = DetectSignedDecoder(rawValue, range);
		if (detected is AxisDecoderKind.NativeSigned or AxisDecoderKind.UnsignedCentered)
		{
			_AxisDecoderKinds[axis] = detected;
		}

		return detected;
	}

	private static AxisDecoderKind DetectSignedDecoder(int rawValue, AxisRange range)
	{
		if (rawValue < 0)
		{
			return AxisDecoderKind.NativeSigned;
		}

		if (rawValue > range.Max)
		{
			return AxisDecoderKind.UnsignedCentered;
		}

		if (rawValue == 0)
		{
			// rawValue == 0 is ambiguous: it is the center of a native-signed axis AND the minimum
			// of an unsigned-centered axis (e.g. a non-auto-centering slider at its rest position).
			// Return Unknown so NormalizeBase falls through to UnsignedCentered, which correctly
			// maps 0 to -1.0. A native-signed axis will cache NativeSigned on the first non-zero read.
			return AxisDecoderKind.Unknown;
		}

		var unsignedCenteredMidpoint = GetUnsignedCenteredMax(range) / 2.0;
		var nativeDistanceFromCenter = rawValue;
		var unsignedCenteredDistanceFromCenter = Math.Abs(rawValue - unsignedCenteredMidpoint);

		if (unsignedCenteredDistanceFromCenter < nativeDistanceFromCenter)
		{
			return AxisDecoderKind.UnsignedCentered;
		}

		if (nativeDistanceFromCenter < unsignedCenteredDistanceFromCenter)
		{
			return AxisDecoderKind.NativeSigned;
		}

		return AxisDecoderKind.Unknown;
	}

	private static double NormalizeSignedFromNativeRange(int rawValue, AxisRange range)
	{
		return NormalizeUnsigned(rawValue, range.Min, range.Max) * 2.0 - 1.0;
	}

	private static double NormalizeSignedFromUnsignedCenteredRange(int rawValue, AxisRange range)
	{
		return NormalizeUnsigned(rawValue, 0, GetUnsignedCenteredMax(range)) * 2.0 - 1.0;
	}

	private static int GetUnsignedCenteredMax(AxisRange range)
	{
		return checked(Math.Max(Math.Abs(range.Min), Math.Abs(range.Max)) * 2 + 1);
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
	private static int EnumObjectsCallback(DirectInputDeviceObjectInstanceNative* instance, nint referenceData)
	{
		var objects = (PooledList<DirectInputDeviceObjectInfo>)GCHandle.FromIntPtr(referenceData).Target!;
		objects.Add(DirectInputDeviceObjectInfo.FromNative(instance));
		return DirectInputNative.DiEnumContinue;
	}

	private sealed class DataFormatScope : IDisposable
	{
		private readonly nint _ObjectBuffer;

		private DataFormatScope(nint objectBuffer, DirectInputDataFormat dataFormat)
		{
			_ObjectBuffer = objectBuffer;
			DataFormat = dataFormat;
		}

		public DirectInputDataFormat DataFormat { get; }

		public void Dispose()
		{
			Marshal.FreeHGlobal(_ObjectBuffer);
		}

		public static DataFormatScope Create(IReadOnlyList<DirectInputObjectDataFormat> objectFormats)
		{
			var objectSize = Marshal.SizeOf<DirectInputObjectDataFormat>();
			var buffer = Marshal.AllocHGlobal(objectFormats.Count * objectSize);

			for (var index = 0; index < objectFormats.Count; index++)
			{
				var target = buffer + index * objectSize;
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
	}

	public static DirectInputJoystickDevice ResolveDevice(string selector)
	{
		using var devices = EnumerateConnected();
		DirectInputJoystickDevice? selected = null;
		try
		{
			if (int.TryParse(selector, out var deviceId))
			{
				selected = devices.FirstOrDefault(device => device.DeviceId == deviceId);
				if (selected is not null)
				{
					return selected;
				}
			}

			var exactMatches = devices
				.Where(device => string.Equals(device.Name, selector, StringComparison.OrdinalIgnoreCase))
				.ToArray();

			if (exactMatches.Length == 1)
			{
				selected = exactMatches[0];
				return selected;
			}

			if (exactMatches.Length > 1)
			{
				throw new InvalidOperationException(
					$"Multiple joystick devices match '{selector}'. Use the numeric id from the list command.");
			}

			var partialMatches = devices
				.Where(device => device.Name.Contains(selector, StringComparison.OrdinalIgnoreCase))
				.ToArray();

			if (partialMatches.Length == 1)
			{
				selected = partialMatches[0];
				return selected;
			}

			if (partialMatches.Length > 1)
			{
				throw new InvalidOperationException(
					$"Multiple joystick devices partially match '{selector}'. Use the full name or numeric id from the list command.");
			}

			throw new InvalidOperationException($"No DirectInput device matched '{selector}'.");
		}
		catch
		{
			DisposeAll(devices);
			throw;
		}
		finally
		{
			DisposeAll(devices, except: selected);
		}
	}

	private static void DisposeAll(IEnumerable<DirectInputJoystickDevice> devices,
		DirectInputJoystickDevice? except = null)
	{
		foreach (var device in devices)
		{
			if (!ReferenceEquals(device, except))
			{
				device.Dispose();
			}
		}
	}
}