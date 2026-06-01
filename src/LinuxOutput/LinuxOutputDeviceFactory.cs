using System.Text;
using Collections.Pooled;

namespace SharpSticks.LinuxOutput;

/// uinput-backed <see cref="IOutputDeviceFactory"/>. <see cref="EnumerateConnectedOutputDevices"/> opens
/// <c>/dev/uinput</c>, declares every axis and button the runtime told us about (so the
/// kernel materializes a device with exactly that capability set), and returns a
/// <see cref="LinuxOutputDevice"/> ready for write traffic.
public sealed class LinuxOutputDeviceFactory : IOutputDeviceFactory<LinuxOutputDevice>, ISupportsOutputSetup
{
	public static LinuxOutputDeviceFactory Instance { get; } = new();

	string ISupportsOutputSetup.SetupSubcommandName => LinuxOutputSetup.SubcommandName;

	void ISupportsOutputSetup.RunSetup(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers) =>
		LinuxOutputSetup.Run(buttonRoutes, axisRoutes, macroButtonNumbers);

	/// Public convenience overload returning concrete <see cref="LinuxOutputDevice"/>
	/// instances. Used by tests / examples that work with the typed factory directly.
	public PooledList<LinuxOutputDevice> EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice>? availableInputs = null)
	{
		var devices = new PooledList<LinuxOutputDevice>(requests.Count);
		try
		{
			foreach (var request in requests)
			{
				devices.Add(OpenOne(request));
			}

			return devices;
		}
		catch
		{
			foreach (var device in devices)
			{
				device.Dispose();
			}

			devices.Dispose();
			throw;
		}
	}

	private static LinuxOutputDevice OpenOne(OutputDeviceRequest request)
	{
		var fd = LinuxLibc.Open(
			LinuxUinput.DevicePath,
			LinuxEventCodes.OWriteOnly | LinuxEventCodes.ONonBlock | LinuxEventCodes.OCloseOnExec);
		if (fd < 0)
		{
			throw new InvalidOperationException(
				$"Failed to open {LinuxUinput.DevicePath} (errno {LinuxLibc.LastError}). " +
				"Run the one-time setup as root, or add your user to the input group.");
		}

		try
		{
			DeclareCapabilities(fd, request.AxisRoutes, request.ButtonRoutes, request.MacroButtonNumbers);
			SetupDevice(fd, request.DeviceId);
			CreateDevice(fd);

			// InputDeviceId stays null on purpose. The matching evdev device only appears
			// AFTER UI_DEV_CREATE here, but the availableInputs snapshot was taken before
			// that — so it can't contain our new counterpart. A consumer that wants to
			// find the new evdev node should re-enumerate inputs after Open returns.
			return new LinuxOutputDevice(
				request.DeviceId,
				fd,
				CollectAxisCodes(request.AxisRoutes),
				CollectButtonCodes(request.ButtonRoutes, request.MacroButtonNumbers));
		}
		catch
		{
			LinuxLibc.Close(fd);
			throw;
		}
	}

	private static void DeclareCapabilities(
		int fd,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<int>? macroButtonNumbers)
	{
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, LinuxEventCodes.EvKey), "UI_SET_EVBIT(EV_KEY)");
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, LinuxEventCodes.EvAbs), "UI_SET_EVBIT(EV_ABS)");
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, LinuxEventCodes.EvSyn), "UI_SET_EVBIT(EV_SYN)");

		foreach (var axis in axisRoutes.Select(static r => r.OutputBinding.Axis).Distinct())
		{
			var code = LinuxOutputAxisCodes.GetAbsCode(axis);
			MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetAbsBit, code), $"UI_SET_ABSBIT({axis})");

			var setup = new LinuxUinputAbsSetup
			{
				Code = code,
				AbsInfo = new LinuxAbsInfo
				{
					Minimum = LinuxOutputDevice.AxisRangeMin,
					Maximum = LinuxOutputDevice.AxisRangeMax,
				},
			};
			MustSucceed(
				LinuxUinputNative.IoctlUinputAbsSetup(fd, LinuxUinput.UiAbsSetup, ref setup),
				$"UI_ABS_SETUP({axis})");
		}

		var hasJoystickRangeButton = false;
		foreach (var buttonNumber in buttonRoutes
			         .Select(static r => r.OutputBinding.ButtonNumber)
			         .Concat(macroButtonNumbers ?? [])
			         .Distinct())
		{
			var code = LinuxOutputAxisCodes.GetButtonCode(buttonNumber);
			MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetKeyBit, code),
				$"UI_SET_KEYBIT({buttonNumber})");

			// Any button in [BTN_JOYSTICK, BTN_GAMEPAD) classifies the device as a joystick,
			// making the baseline below unnecessary.
			hasJoystickRangeButton |= code is >= LinuxEventCodes.BtnJoystick and < LinuxEventCodes.BtnGamepad;
		}

		// udev's input_id and SDL need a BTN_JOYSTICK-range key to set ID_INPUT_JOYSTICK; without
		// one, an axis-only profile works in evtest but is ignored by SDL/Steam Input/games. Add a
		// baseline only when no routed button covers it, so we never inject a phantom button.
		if (!hasJoystickRangeButton)
		{
			MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetKeyBit, LinuxEventCodes.BtnJoystick),
				"UI_SET_KEYBIT(BTN_JOYSTICK baseline)");
		}
	}

	private static void SetupDevice(int fd, uint deviceId)
	{
		var setup = new LinuxUinputSetup
		{
			Id = new LinuxInputId
			{
				BusType = LinuxUinput.BusVirtual,
				Vendor = 0xfeed,
				Product = (ushort)(0xc000 | (deviceId & 0xff)),
				Version = 0x0100,
			},
			FfEffectsMax = 0,
		};

		WriteName(ref setup, $"SharpSticks Virtual Joystick {deviceId}");

		MustSucceed(
			LinuxUinputNative.IoctlUinputSetup(fd, LinuxUinput.UiDevSetup, ref setup),
			"UI_DEV_SETUP");
	}

	private static unsafe void WriteName(ref LinuxUinputSetup setup, string name)
	{
		Span<byte> nameBuffer = new(Unsafe.AsPointer(ref setup.Name[0]), LinuxUinput.MaxNameSize);
		nameBuffer.Clear();
		var bytes = Encoding.UTF8.GetBytes(name);
		var copyLength = Math.Min(bytes.Length, LinuxUinput.MaxNameSize - 1);
		bytes.AsSpan(0, copyLength).CopyTo(nameBuffer);
	}

	private static void CreateDevice(int fd)
	{
		MustSucceed(LinuxLibc.IoctlNoArg(fd, LinuxUinput.UiDevCreate), "UI_DEV_CREATE");
	}

	private static FrozenDictionary<Axis, ushort> CollectAxisCodes(IReadOnlyCollection<AxisRoute> axisRoutes)
	{
		var dict = new Dictionary<Axis, ushort>();
		foreach (var axis in axisRoutes.Select(static r => r.OutputBinding.Axis).Distinct())
		{
			dict[axis] = LinuxOutputAxisCodes.GetAbsCode(axis);
		}

		return dict.ToFrozenDictionary();
	}

	private static FrozenDictionary<int, ushort> CollectButtonCodes(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<int>? macroButtonNumbers)
	{
		var dict = new Dictionary<int, ushort>();
		foreach (var buttonNumber in buttonRoutes
			         .Select(static r => r.OutputBinding.ButtonNumber)
			         .Concat(macroButtonNumbers ?? [])
			         .Distinct())
		{
			dict[buttonNumber] = LinuxOutputAxisCodes.GetButtonCode(buttonNumber);
		}

		return dict.ToFrozenDictionary();
	}

	private static void MustSucceed(int ioctlResult, string what)
	{
		if (ioctlResult < 0)
		{
			throw new InvalidOperationException(
				$"uinput {what} failed (errno {LinuxLibc.LastError}).");
		}
	}
}
