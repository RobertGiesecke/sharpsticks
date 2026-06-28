using System.Runtime.InteropServices;

namespace SharpSticks.InputSynthesis.Windows;

/// <summary>
/// Thin <c>user32!SendInput</c> P/Invoke layer. AOT-friendly via
/// <see cref="LibraryImportAttribute"/>. One event is sent per call; the higher
/// layers decide flags and codes.
/// </summary>
internal static partial class Win32Input
{
	public const uint InputMouse = 0;
	public const uint InputKeyboard = 1;

	public const uint KeyEventKeyUp = 0x0002;
	public const uint KeyEventScancode = 0x0008;
	public const uint KeyEventExtendedKey = 0x0001;

	public const uint MouseEventLeftDown = 0x0002;
	public const uint MouseEventLeftUp = 0x0004;
	public const uint MouseEventRightDown = 0x0008;
	public const uint MouseEventRightUp = 0x0010;
	public const uint MouseEventMiddleDown = 0x0020;
	public const uint MouseEventMiddleUp = 0x0040;
	public const uint MouseEventMove = 0x0001;
	public const uint MouseEventXDown = 0x0080;
	public const uint MouseEventXUp = 0x0100;
	public const uint MouseEventWheel = 0x0800;
	public const uint MouseEventHWheel = 0x01000;

	/// <summary>WHEEL_DELTA: the wheel-data magnitude of one detent.</summary>
	public const int WheelDelta = 120;

	public const uint XButton1 = 0x0001;
	public const uint XButton2 = 0x0002;

	[StructLayout(LayoutKind.Sequential)]
	public struct Input
	{
		public uint Type;
		public InputUnion Union;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct InputUnion
	{
		// ReSharper disable once MemberHidesStaticFromOuterClass
		[FieldOffset(0)] public MouseInput Mouse;
		[FieldOffset(0)] public KeyboardInput Keyboard;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct KeyboardInput
	{
		public ushort Vk;
		public ushort Scan;
		public uint Flags;
		public uint Time;
		public nuint ExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MouseInput
	{
		public int Dx;
		public int Dy;
		public uint MouseData;
		public uint Flags;
		public uint Time;
		public nuint ExtraInfo;
	}

	[LibraryImport("user32.dll", SetLastError = true)]
	private static partial uint SendInput(uint nInputs, ref Input pInputs, int cbSize);

	public static void Send(in Input input)
	{
		var copy = input;
		_ = SendInput(1, ref copy, Marshal.SizeOf<Input>());
	}

	public static Input KeyByScancode(ushort scancode, bool extended, bool up)
	{
		var flags = KeyEventScancode;
		if (extended)
		{
			flags |= KeyEventExtendedKey;
		}

		if (up)
		{
			flags |= KeyEventKeyUp;
		}

		return new()
		{
			Type = InputKeyboard,
			Union = new() { Keyboard = new() { Scan = scancode, Flags = flags } },
		};
	}

	public static Input KeyByVirtualKey(ushort vk, bool up)
	{
		var flags = KeyEventExtendedKey;
		if (up)
		{
			flags |= KeyEventKeyUp;
		}

		return new()
		{
			Type = InputKeyboard,
			Union = new() { Keyboard = new() { Vk = vk, Flags = flags } },
		};
	}

	public static Input Mouse(uint flags, uint mouseData = 0) =>
		new()
		{
			Type = InputMouse,
			Union = new() { Mouse = new() { Flags = flags, MouseData = mouseData } },
		};

	public static Input MouseMove(int dx, int dy) =>
		new()
		{
			Type = InputMouse,
			Union = new() { Mouse = new() { Dx = dx, Dy = dy, Flags = MouseEventMove } },
		};

	/// <summary>A wheel event carrying an already-scaled (signed) wheel delta.</summary>
	public static Input MouseWheel(int delta, bool horizontal) =>
		Mouse(horizontal ? MouseEventHWheel : MouseEventWheel, unchecked((uint)delta));
}
