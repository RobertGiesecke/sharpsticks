using System.Text;

namespace SharpSticks.LinuxOutput;

/// uinput-backed <see cref="IOutputDeviceFactory"/>. <see cref="Open"/> opens
/// <c>/dev/uinput</c>, declares every axis and button the runtime told us about (so the
/// kernel materializes a device with exactly that capability set), and returns a
/// <see cref="LinuxOutputDevice"/> ready for write traffic.
public sealed class LinuxOutputDeviceFactory : IOutputDeviceFactory, ISupportsOutputSetup
{
	public static LinuxOutputDeviceFactory Instance { get; } = new();

	string ISupportsOutputSetup.SetupSubcommandName => LinuxOutputSetup.SubcommandName;

	void ISupportsOutputSetup.RunSetup(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers) =>
		LinuxOutputSetup.Run(buttonRoutes, axisRoutes, macroButtonNumbers);

	OutputDevice IOutputDeviceFactory.Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers) =>
		Open(deviceId, buttonRoutes, axisRoutes, macroButtonNumbers);

	public LinuxOutputDevice Open(
		uint deviceId,
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int>? macroButtonNumbers = null)
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
			DeclareCapabilities(fd, axisRoutes, buttonRoutes, macroButtonNumbers);
			SetupDevice(fd, deviceId);
			CreateDevice(fd);

			return new LinuxOutputDevice(
				deviceId,
				fd,
				CollectAxisCodes(axisRoutes),
				CollectButtonCodes(buttonRoutes, macroButtonNumbers));
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

		foreach (var buttonNumber in buttonRoutes
			         .Select(static r => r.OutputBinding.ButtonNumber)
			         .Concat(macroButtonNumbers ?? [])
			         .Distinct())
		{
			var code = LinuxOutputAxisCodes.GetButtonCode(buttonNumber);
			MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetKeyBit, code),
				$"UI_SET_KEYBIT({buttonNumber})");
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
