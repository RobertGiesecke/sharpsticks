using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputSynthesis.Windows;

/// <summary>
/// Windows <see cref="IInputSynthesizer"/> backed by <c>SendInput</c>. Keys are
/// dispatched by HID usage page: the keyboard page (0x07) goes through
/// <see cref="ScancodeKeyEmitter"/> (scancode injection — what games read), the
/// consumer page (0x0C) through <see cref="ConsumerKeyEmitter"/> (virtual keys).
/// Mouse buttons map straight onto SendInput's mouse flags. <c>SendInput</c> emits
/// eagerly, so <see cref="Flush"/> is a no-op.
///
/// <para>The emitters build the events; the actual send is an injected delegate
/// (real <c>SendInput</c> by default) so tests can capture the events instead of
/// driving the OS.</para>
/// </summary>
public sealed class WindowsInputSynthesizer : IInputSynthesizer
{
	public static readonly WindowsInputSynthesizer Instance = new();

	private readonly Action<Win32Input.Input> _Send;

	public WindowsInputSynthesizer()
		: this(static input => Win32Input.Send(input))
	{
	}

	// Test seam: capture the events that would be sent without touching the OS.
	internal WindowsInputSynthesizer(Action<Win32Input.Input> send) => _Send = send;

	public void KeyDown(Key key) => EmitKey(key, down: true);
	public void KeyUp(Key key) => EmitKey(key, down: false);

	private void EmitKey(Key key, bool down)
	{
		var built = key.UsagePage switch
		{
			Key.KeyboardUsagePage => ScancodeKeyEmitter.TryBuild(key, down),
			Key.ConsumerUsagePage => ConsumerKeyEmitter.TryBuild(key, down),
			_ => throw new NotSupportedException(
				$"Unsupported HID usage page 0x{key.UsagePage:X} for key {key}."),
		};

		_Send(built ?? throw new NotSupportedException(
			$"No mapping for HID usage 0x{key.Usage:X} on page 0x{key.UsagePage:X} ({key})."));
	}

	public void MouseButtonDown(OutputMouseButton button) => _Send(BuildMouse(button, down: true));
	public void MouseButtonUp(OutputMouseButton button) => _Send(BuildMouse(button, down: false));

	public void MoveMouseRelative(int dx, int dy) => _Send(Win32Input.MouseMove(dx, dy));

	internal static Win32Input.Input BuildMouse(OutputMouseButton button, bool down) => button switch
	{
		OutputMouseButton.Left =>
			Win32Input.Mouse(down ? Win32Input.MouseEventLeftDown : Win32Input.MouseEventLeftUp),
		OutputMouseButton.Right =>
			Win32Input.Mouse(down ? Win32Input.MouseEventRightDown : Win32Input.MouseEventRightUp),
		OutputMouseButton.Middle =>
			Win32Input.Mouse(down ? Win32Input.MouseEventMiddleDown : Win32Input.MouseEventMiddleUp),
		OutputMouseButton.X1 =>
			Win32Input.Mouse(down ? Win32Input.MouseEventXDown : Win32Input.MouseEventXUp, Win32Input.XButton1),
		OutputMouseButton.X2 =>
			Win32Input.Mouse(down ? Win32Input.MouseEventXDown : Win32Input.MouseEventXUp, Win32Input.XButton2),
		_ => throw new NotSupportedException($"Unsupported mouse button {button}."),
	};

	public void Flush()
	{
	}
}
