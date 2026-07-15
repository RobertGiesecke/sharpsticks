using System.Collections.Immutable;
using System.Text;
using Collections.Pooled;
using SharpSticks.LinuxInput;

namespace SharpSticks.LinuxOutput;

/// uinput-backed <see cref="IOutputDeviceFactory"/>. <see cref="EnumerateConnectedOutputDevices"/> opens
/// <c>/dev/uinput</c>, declares every axis and button the runtime told us about (so the
/// kernel materializes a device with exactly that capability set), and returns a
/// <see cref="LinuxOutputDevice"/> ready for write traffic.
public sealed class LinuxOutputDeviceFactory : IOutputDeviceFactory<LinuxOutputDevice>, ISupportsOutputSetup
{
	public static LinuxOutputDeviceFactory Instance { get; } = new();

	// Identity stamped on every virtual device (see SetupDevice). The same (vendor, product)
	// lets us find the freshly-created evdev node again after UI_DEV_CREATE and read back the
	// DeviceId the input enumerator assigns it, so InputDeviceId correlates output ↔ input.
	private const ushort VirtualVendor = 0xfeed;

	private static ushort VirtualProduct(uint deviceId) => (ushort)(0xc000 | (deviceId & 0xff));

	private static uint DeviceIdFromProduct(ushort product) => (uint)(product & 0xff);

	private static bool IsVirtualProduct(ushort vendor, ushort product) =>
		vendor == VirtualVendor && (product & 0xff00) == 0xc000;

	// uinput has no persistent slots to enumerate — a device exists only while some process holds
	// it open. But a live SharpSticks output surfaces as an evdev input stamped with our (vendor,
	// product), so we recover it from the input list: that gives the source generator the real
	// name + product GUID, and lets it fold the input-side loopback into the output instead of
	// listing it as a separate device.
	public ImmutableArray<AvailableOutputDevice> EnumerateAvailableOutputs()
	{
		var builder = ImmutableArray.CreateBuilder<AvailableOutputDevice>();
		using var seenIds = new PooledSet<uint>();
		foreach (var input in LinuxInputJoystickDeviceFactory.Instance.EnumerateAvailableInputs())
		{
			// Two processes can each hold a device with the same id; surface each id once so the
			// generator (and any consumer keyed by id) doesn't see duplicates.
			if (ProductGuidEncoder.TryDecode(input.ProductGuid, out var vendor, out var product)
			    && IsVirtualProduct(vendor, product)
			    && seenIds.Add(DeviceIdFromProduct(product)))
			{
				builder.Add(new(
					DeviceIdFromProduct(product), input.Axes, input.ButtonCount, input.ProductGuid, input.ProductName));
			}
		}

		return builder.ToImmutable();
	}

	// Identity a declared output id will have once created — deterministic from the id, so the
	// generator can map a declared device even when it isn't live at build time.
	public AvailableOutputDevice DescribeDeclaredOutput(uint deviceId, ImmutableArray<Axis> axes, uint buttonCount) =>
		new(deviceId, axes, buttonCount,
			ProductGuidEncoder.Encode(VirtualVendor, VirtualProduct(deviceId)),
			$"SharpSticks Virtual Joystick {deviceId}");

	string ISupportsOutputSetup.SetupSubcommandName => LinuxOutputSetup.SubcommandName;
	
	public IInputSynthesizer InputSynthesizer => LinuxInputSynthesizer.Instance;

	void ISupportsOutputSetup.RunSetup(
		IReadOnlyCollection<OutputButtonBinding> outputButtons,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers) =>
		LinuxOutputSetup.Run(outputButtons, axisRoutes, macroButtonNumbers);

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
			OpenFlags.WriteOnly | OpenFlags.NonBlock | OpenFlags.CloseOnExec);
		if (fd < 0)
		{
			throw new InvalidOperationException(
				$"Failed to open {LinuxUinput.DevicePath} (errno {LinuxLibc.LastError}). " +
				"Run the one-time setup as root, or add your user to the input group.");
		}

		try
		{
			DeclareCapabilities(fd, request.AxisRoutes, request.OutputButtons, request.MacroButtonNumbers,
				request.ButtonCount, request.DeclaredAxes);
			SetupDevice(fd, request.DeviceId);
			CreateDevice(fd);

			// The matching evdev node only exists AFTER UI_DEV_CREATE, so it couldn't be in
			// the availableInputs snapshot (taken before). Re-enumerate now and find our node
			// by the (vendor, product) identity we just stamped, reading back the DeviceId the
			// input enumerator assigns it — null if it isn't visible yet.
			var inputDeviceId = DiscoverInputDeviceId(request.DeviceId);

			return new(
				request.DeviceId,
				fd,
				CollectAxisCodes(request.AxisRoutes),
				CollectButtonCodes(request.OutputButtons, request.MacroButtonNumbers),
				inputDeviceId);
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
		IReadOnlyCollection<OutputButtonBinding> outputButtons,
		IReadOnlyCollection<int>? macroButtonNumbers,
		uint declaredButtonCount,
		ImmutableArray<Axis> declaredAxes)
	{
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, EvType.Key.ToNative()), "UI_SET_EVBIT(EV_KEY)");
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, EvType.Abs.ToNative()), "UI_SET_EVBIT(EV_ABS)");
		MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetEvBit, EvType.Syn.ToNative()), "UI_SET_EVBIT(EV_SYN)");

		// Declared axes (from [OutputDevice]) unioned with routed axes, so the device advertises
		// its full axis set even for axes no route drives.
		var axes = axisRoutes.Select(static r => r.OutputBinding.Axis);
		if (!declaredAxes.IsDefaultOrEmpty)
		{
			axes = axes.Concat(declaredAxes);
		}

		foreach (var axis in axes.Distinct())
		{
			var code = LinuxOutputAxisCodes.GetAbsCode(axis);
			MustSucceed(LinuxLibc.IoctlInt(fd, LinuxUinput.UiSetAbsBit, code), $"UI_SET_ABSBIT({axis})");

			var setup = new LinuxUinputAbsSetup
			{
				Code = code,
				AbsInfo = new()
				{
					Minimum = LinuxOutputDevice.AxisRangeMin,
					Maximum = LinuxOutputDevice.AxisRangeMax,
				},
			};
			MustSucceed(
				LinuxUinputNative.IoctlUinputAbsSetup(fd, LinuxUinput.UiAbsSetup, ref setup),
				$"UI_ABS_SETUP({axis})");
		}

		// With a declared button count, materialize a dense 1..N button block (merged in the
		// runtime to cover every routed button too). A dense block is what makes button N land at
		// evdev index N — a sparse set would renumber by rank. Without a declaration, fall back to
		// exactly the routed buttons. The declared count is clamped to what this backend can
		// represent: a declaration of "up to N" buttons past the ceiling (e.g. vJoy's 128 reused on
		// Linux) is honoured as far as the platform allows instead of throwing at GetButtonCode.
		var denseCount = Math.Min(declaredButtonCount, LinuxOutputAxisCodes.MaxButtons);
		var buttonNumbers = denseCount > 0
			? Enumerable.Range(1, (int)denseCount)
			: outputButtons.Select(static b => b.ButtonNumber).Concat(macroButtonNumbers ?? []).Distinct();

		var hasJoystickRangeButton = false;
		foreach (var buttonNumber in buttonNumbers)
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
			Id = new()
			{
				BusType = LinuxUinput.BusVirtual,
				Vendor = VirtualVendor,
				Product = VirtualProduct(deviceId),
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

	/// Finds the just-created uinput device among the enumerated evdev inputs by its stamped
	/// (vendor, product) identity and returns the DeviceId the input enumerator assigned it,
	/// or null if it can't be found. That id is a positional index over the current input set,
	/// so it correlates only against an input enumeration of the same device set (which is how
	/// consumers re-discover these outputs); it is not a stable per-device identifier.
	private static int? DiscoverInputDeviceId(uint deviceId)
	{
		var expected = ProductGuidEncoder.Encode(VirtualVendor, VirtualProduct(deviceId));
		foreach (var input in LinuxInputJoystickDeviceFactory.Instance.EnumerateAvailableInputs())
		{
			if (input.ProductGuid == expected)
			{
				return input.DeviceId;
			}
		}

		return null;
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
		IReadOnlyCollection<OutputButtonBinding> outputButtons,
		IReadOnlyCollection<int>? macroButtonNumbers)
	{
		var dict = new Dictionary<int, ushort>();
		foreach (var buttonNumber in outputButtons
			         .Select(static b => b.ButtonNumber)
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
