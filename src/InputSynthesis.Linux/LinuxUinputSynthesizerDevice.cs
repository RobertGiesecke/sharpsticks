using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpSticks.InputSynthesis.Linux;

/// <summary>
/// A uinput virtual keyboard+mouse the synthesizer writes to. Created once, it
/// registers the full evdev key range plus the mouse buttons up front (uinput
/// requires every emittable code be declared before <c>UI_DEV_CREATE</c>), then
/// accepts <c>EV_KEY</c> writes which the kernel applies on the next
/// <c>SYN_REPORT</c>. The device is torn down on <see cref="Dispose"/> (and, as a
/// backstop, when the process exits and the fd closes).
/// </summary>
internal sealed partial class LinuxUinputSynthesizerDevice : ILinuxInputEventSender, IDisposable
{
	private const string DevicePath = "/dev/uinput";
	private const int MaxNameSize = 80;
	private const ushort BusVirtual = 0x06;

	// Highest evdev key code (KEY_MAX); the BTN_* mouse codes fall inside this range.
	private const int KeyMax = 0x2ff;

	private const uint IocType = (uint)'U';
	private static uint UiDevCreate => LinuxIoctl.Encode(LinuxIoctl.DirNone, IocType, 1, 0);
	private static uint UiDevDestroy => LinuxIoctl.Encode(LinuxIoctl.DirNone, IocType, 2, 0);
	private static uint UiDevSetup => LinuxIoctl.Iow(IocType, 3, UinputSetup.Size);
	private static uint UiSetEvBit => LinuxIoctl.Iow(IocType, 100, sizeof(int));
	private static uint UiSetKeyBit => LinuxIoctl.Iow(IocType, 101, sizeof(int));

	private int _Fd = -1;
	private bool _Initialized;
	private bool _Disposed;

	/// <summary>
	/// Opens <c>/dev/uinput</c>, registers the key range and creates the device.
	/// Idempotent — a second call is a no-op.
	/// </summary>
	public void Initialize()
	{
		if (_Initialized)
		{
			return;
		}

		var fd = LinuxLibc.Open(DevicePath, OpenFlags.WriteOnly | OpenFlags.NonBlock);
		if (fd < 0)
		{
			throw new InvalidOperationException(
				$"Could not open {DevicePath} (errno {LinuxLibc.LastError}). " +
				"Is the uinput module loaded and accessible? See the Linux output setup step.");
		}

		try
		{
			Check(LinuxLibc.IoctlInt(fd, UiSetEvBit, (int)EvType.Key), "UI_SET_EVBIT(EV_KEY)");
			for (var code = 1; code <= KeyMax; code++)
			{
				Check(LinuxLibc.IoctlInt(fd, UiSetKeyBit, code), "UI_SET_KEYBIT");
			}

			var setup = new UinputSetup
			{
				Id = new LinuxInputId { BusType = BusVirtual, Vendor = 0x1209, Product = 0x5350, Version = 1 },
				FfEffectsMax = 0,
			};
			SetName(ref setup, "SharpSticks Synthetic Keyboard+Mouse");
			Check(IoctlUinputSetup(fd, UiDevSetup, ref setup), "UI_DEV_SETUP");
			Check(LinuxLibc.IoctlNoArg(fd, UiDevCreate), "UI_DEV_CREATE");
		}
		catch
		{
			LinuxLibc.Close(fd);
			throw;
		}

		_Fd = fd;
		_Initialized = true;
	}

	public void Write(LinuxInputEvent ev)
	{
		if (_Disposed)
		{
			return;
		}

		// Lazy fallback for when the build opted out of start-time initialization.
		Initialize();

		var size = (nuint)LinuxInputEvent.Size;
		var written = LinuxLibc.Write(_Fd, ref Unsafe.As<LinuxInputEvent, byte>(ref ev), size);
		if (written != (nint)size)
		{
			throw new InvalidOperationException(
				$"uinput write returned {written} (errno {LinuxLibc.LastError}).");
		}
	}

	public void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		if (_Initialized)
		{
			LinuxLibc.IoctlNoArg(_Fd, UiDevDestroy);
			LinuxLibc.Close(_Fd);
		}
	}

	private static void Check(int result, string what)
	{
		if (result < 0)
		{
			throw new InvalidOperationException($"uinput {what} failed (errno {LinuxLibc.LastError}).");
		}
	}

	private static unsafe void SetName(ref UinputSetup setup, string name)
	{
		var bytes = Encoding.ASCII.GetBytes(name);
		var count = Math.Min(bytes.Length, MaxNameSize - 1);
		fixed (byte* dst = setup.Name)
		{
			bytes.AsSpan(0, count).CopyTo(new Span<byte>(dst, MaxNameSize));
			dst[count] = 0;
		}
	}

	[LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
	private static partial int IoctlUinputSetup(int fd, nuint request, ref UinputSetup value);

	/// <c>struct uinput_setup</c>: input_id (8) + name[80] + ff_effects_max (4) = 92 bytes.
	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct UinputSetup
	{
		public LinuxInputId Id;
		public fixed byte Name[MaxNameSize];
		public uint FfEffectsMax;

		public const uint Size = LinuxInputId.Size + MaxNameSize + sizeof(uint);
	}
}
