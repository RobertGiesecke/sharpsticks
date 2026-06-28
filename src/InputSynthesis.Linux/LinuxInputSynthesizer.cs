using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputSynthesis.Linux;

/// <summary>
/// Linux <see cref="IInputSynthesizer"/> backed by a uinput virtual device. Keys
/// dispatch by HID usage page: the keyboard page (0x07) through
/// <see cref="EvdevKeyEmitter"/>, the consumer page (0x0C) through
/// <see cref="EvdevConsumerKeyEmitter"/>; mouse buttons map onto evdev <c>BTN_*</c>
/// codes. Each <c>KeyDown</c>/<c>KeyUp</c> writes an <c>EV_KEY</c> event; the
/// kernel only applies them on the <c>SYN_REPORT</c> that <see cref="Flush"/>
/// emits once per frame — so a frame's key changes commit atomically.
///
/// <para>The uinput device is created lazily on first use (so merely building a
/// runtime — or doing so on a non-Linux host — never opens <c>/dev/uinput</c>),
/// and the send step is an injected delegate so tests capture events instead of
/// driving the kernel.</para>
/// </summary>
public sealed class LinuxInputSynthesizer : IInputSynthesizer, IDisposable
{
	public static readonly LinuxInputSynthesizer Instance = new(
		new LinuxUinputSynthesizerDevice(),
		// default instance should not dispose the device, since this instance is shared
		ownsInputEventSender: false);

	private readonly ILinuxInputEventSender _InputEventSender;
	private readonly bool _OwnsInputEventSender;
	private bool _PendingSinceFlush;

	// High-res scroll accumulators: hi-res events report in 1/120-notch units, but
	// legacy clients only read REL_WHEEL/REL_HWHEEL, so we also emit a whole notch
	// each time the accumulated hi-res amount crosses a detent.
	private int _WheelHiResAccumulator;
	private int _HWheelHiResAccumulator;

	public LinuxInputSynthesizer() : this(new LinuxUinputSynthesizerDevice(), ownsInputEventSender: true)
	{
	}

	// Test seam: capture the events that would be written, without touching uinput.
	internal LinuxInputSynthesizer(
		ILinuxInputEventSender inputEventSender,
		bool ownsInputEventSender)
	{
		_InputEventSender = inputEventSender;
		_OwnsInputEventSender = ownsInputEventSender;
	}

	public void EnsureInitialized() => _InputEventSender.Initialize();

	public void KeyDown(Key key) => EmitKey(key, down: true);
	public void KeyUp(Key key) => EmitKey(key, down: false);

	private void EmitKey(Key key, bool down)
	{
		var built = key.UsagePage switch
		{
			Key.KeyboardUsagePage => EvdevKeyEmitter.TryBuild(key, down),
			Key.ConsumerUsagePage => EvdevConsumerKeyEmitter.TryBuild(key, down),
			_ => throw new NotSupportedException(
				$"Unsupported HID usage page 0x{key.UsagePage:X} for key {key}."),
		};

		Emit(built ?? throw new NotSupportedException(
			$"No mapping for HID usage 0x{key.Usage:X} on page 0x{key.UsagePage:X} ({key})."));
	}

	public void MouseButtonDown(OutputMouseButton button) => Emit(EvdevEvent.Key(MouseCode(button), down: true));
	public void MouseButtonUp(OutputMouseButton button) => Emit(EvdevEvent.Key(MouseCode(button), down: false));

	public void MoveMouseRelative(int dx, int dy)
	{
		if (dx != 0)
		{
			Emit(EvdevEvent.Rel(EvdevEvent.RelX, dx));
		}

		if (dy != 0)
		{
			Emit(EvdevEvent.Rel(EvdevEvent.RelY, dy));
		}
	}

	public void Scroll(int vertical, int horizontal, MouseScrollUnit unit = MouseScrollUnit.Notch)
	{
		if (unit == MouseScrollUnit.Notch)
		{
			if (vertical != 0)
			{
				Emit(EvdevEvent.Rel(EvdevEvent.RelWheel, vertical));
			}

			if (horizontal != 0)
			{
				Emit(EvdevEvent.Rel(EvdevEvent.RelHWheel, horizontal));
			}

			return;
		}

		ScrollHiRes(vertical, EvdevEvent.RelWheelHiRes, EvdevEvent.RelWheel, ref _WheelHiResAccumulator);
		ScrollHiRes(horizontal, EvdevEvent.RelHWheelHiRes, EvdevEvent.RelHWheel, ref _HWheelHiResAccumulator);
	}

	private void ScrollHiRes(int amount, ushort hiResCode, ushort notchCode, ref int accumulator)
	{
		if (amount == 0)
		{
			return;
		}

		Emit(EvdevEvent.Rel(hiResCode, amount));

		accumulator += amount;
		while (accumulator >= EvdevEvent.WheelHiResPerNotch)
		{
			Emit(EvdevEvent.Rel(notchCode, 1));
			accumulator -= EvdevEvent.WheelHiResPerNotch;
		}

		while (accumulator <= -EvdevEvent.WheelHiResPerNotch)
		{
			Emit(EvdevEvent.Rel(notchCode, -1));
			accumulator += EvdevEvent.WheelHiResPerNotch;
		}
	}

	private static ushort MouseCode(OutputMouseButton button) => button switch
	{
		OutputMouseButton.Left => EvdevEvent.BtnLeft,
		OutputMouseButton.Right => EvdevEvent.BtnRight,
		OutputMouseButton.Middle => EvdevEvent.BtnMiddle,
		OutputMouseButton.X1 => EvdevEvent.BtnSide,
		OutputMouseButton.X2 => EvdevEvent.BtnExtra,
		_ => throw new NotSupportedException($"Unsupported mouse button {button}."),
	};

	private void Emit(LinuxInputEvent ev)
	{
		_InputEventSender.Write(ev);
		_PendingSinceFlush = true;
	}

	public void Flush()
	{
		if (!_PendingSinceFlush)
		{
			return;
		}

		_InputEventSender.Write(EvdevEvent.SynReport());
		_PendingSinceFlush = false;
	}

	public void Dispose()
	{
		if(_OwnsInputEventSender && _InputEventSender is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
