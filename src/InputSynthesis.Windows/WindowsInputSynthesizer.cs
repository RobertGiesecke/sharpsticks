using SharpSticks.InputAbstractions;
using SharpSticks.InputAbstractions.Keyboard;
using SharpSticks.InputAbstractions.Mouse;

namespace SharpSticks.InputSynthesis.Windows;

/// <summary>
/// Windows <see cref="IInputSynthesizer"/> backed by <c>SendInput</c>. Keys are
/// dispatched by HID usage page: the keyboard page (0x07) goes through
/// <see cref="ScancodeKeyEmitter"/> (scancode injection — what games read), the
/// consumer page (0x0C) through <see cref="ConsumerKeyEmitter"/> (virtual keys).
/// Mouse buttons map straight onto SendInput's mouse flags. <c>SendInput</c> emits
/// eagerly, so <see cref="Flush"/> is a no-op.
/// </summary>
public sealed class WindowsInputSynthesizer : IInputSynthesizer
{
	public static readonly WindowsInputSynthesizer Instance = new();

	public void KeyDown(Key key) => EmitKey(key, down: true);
	public void KeyUp(Key key) => EmitKey(key, down: false);

	private static void EmitKey(Key key, bool down)
	{
		switch (key.UsagePage)
		{
			case Key.KeyboardUsagePage:
				ScancodeKeyEmitter.Emit(key, down);
				break;
			case Key.ConsumerUsagePage:
				ConsumerKeyEmitter.Emit(key, down);
				break;
			default:
				throw new NotSupportedException(
					$"Unsupported HID usage page 0x{key.UsagePage:X} for key {key}.");
		}
	}

	public void MouseButtonDown(OutputMouseButton button) => EmitMouse(button, down: true);
	public void MouseButtonUp(OutputMouseButton button) => EmitMouse(button, down: false);

	private static void EmitMouse(OutputMouseButton button, bool down)
	{
		switch (button)
		{
			case OutputMouseButton.Left:
				Win32Input.Send(Win32Input.Mouse(down ? Win32Input.MouseEventLeftDown : Win32Input.MouseEventLeftUp));
				break;
			case OutputMouseButton.Right:
				Win32Input.Send(Win32Input.Mouse(down ? Win32Input.MouseEventRightDown : Win32Input.MouseEventRightUp));
				break;
			case OutputMouseButton.Middle:
				Win32Input.Send(Win32Input.Mouse(down ? Win32Input.MouseEventMiddleDown : Win32Input.MouseEventMiddleUp));
				break;
			case OutputMouseButton.X1:
				Win32Input.Send(Win32Input.Mouse(down ? Win32Input.MouseEventXDown : Win32Input.MouseEventXUp, Win32Input.XButton1));
				break;
			case OutputMouseButton.X2:
				Win32Input.Send(Win32Input.Mouse(down ? Win32Input.MouseEventXDown : Win32Input.MouseEventXUp, Win32Input.XButton2));
				break;
			default:
				throw new NotSupportedException($"Unsupported mouse button {button}.");
		}
	}

	public void Flush()
	{
	}
}
