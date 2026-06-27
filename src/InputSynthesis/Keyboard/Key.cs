namespace SharpSticks.InputSynthesis.Keyboard;

/// <summary>
/// Any keyboard key, named or not — the value type bindings actually carry.
/// <see cref="Code"/> packs a USB HID usage as <c>(UsagePage &lt;&lt; 16) | Usage</c>,
/// so the keyboard page (0x07) and the consumer page (0x0C, media/browser keys)
/// share one space without colliding. Use the <see cref="NamedKey"/> enum for the
/// recognizable keys (it converts implicitly); reach anything unnamed or
/// vendor-specific through <see cref="FromKeyboard"/> / <see cref="FromConsumer"/>
/// so the curated enum never has to grow an ambiguous member.
/// </summary>
public readonly record struct Key(int Code)
{
	/// <summary>HID Keyboard/Keypad usage page.</summary>
	public const int KeyboardUsagePage = 0x07;

	/// <summary>HID Consumer usage page (media, browser, launch keys).</summary>
	public const int ConsumerUsagePage = 0x0C;

	internal const int KeyboardBase = KeyboardUsagePage << 16;
	internal const int ConsumerBase = ConsumerUsagePage << 16;

	/// <summary>HID usage page this key lives on (e.g. 0x07 keyboard, 0x0C consumer).</summary>
	public int UsagePage => (Code >> 16) & 0xFFFF;

	/// <summary>HID usage id within <see cref="UsagePage"/>.</summary>
	public int Usage => Code & 0xFFFF;

	/// <summary>A key on the HID keyboard/keypad page (0x07) by raw usage id.</summary>
	public static Key FromKeyboard(int usage) => new(KeyboardBase | (usage & 0xFFFF));

	/// <summary>A key on the HID consumer page (0x0C) by raw usage id.</summary>
	public static Key FromConsumer(int usage) => new(ConsumerBase | (usage & 0xFFFF));

	public static implicit operator Key(NamedKey named) => new((int)named);

	public override string ToString() =>
		TryGetEnumName((NamedKey)Code) ?? $"Key(0x{Code:X})";

	// A jump-table lookup of the curated names — avoids Enum.IsDefined + Enum.ToString
	// (reflection/metadata) on the ToString path, and every arm is an interned
	// nameof constant so it allocates nothing. Keep in sync with NamedKey; an
	// unlisted member just falls back to the Key(0x…) form, no error.
	private static string? TryGetEnumName(NamedKey key) => key switch
	{
		NamedKey.A => nameof(NamedKey.A), NamedKey.B => nameof(NamedKey.B), NamedKey.C => nameof(NamedKey.C),
		NamedKey.D => nameof(NamedKey.D), NamedKey.E => nameof(NamedKey.E), NamedKey.F => nameof(NamedKey.F),
		NamedKey.G => nameof(NamedKey.G), NamedKey.H => nameof(NamedKey.H), NamedKey.I => nameof(NamedKey.I),
		NamedKey.J => nameof(NamedKey.J), NamedKey.K => nameof(NamedKey.K), NamedKey.L => nameof(NamedKey.L),
		NamedKey.M => nameof(NamedKey.M), NamedKey.N => nameof(NamedKey.N), NamedKey.O => nameof(NamedKey.O),
		NamedKey.P => nameof(NamedKey.P), NamedKey.Q => nameof(NamedKey.Q), NamedKey.R => nameof(NamedKey.R),
		NamedKey.S => nameof(NamedKey.S), NamedKey.T => nameof(NamedKey.T), NamedKey.U => nameof(NamedKey.U),
		NamedKey.V => nameof(NamedKey.V), NamedKey.W => nameof(NamedKey.W), NamedKey.X => nameof(NamedKey.X),
		NamedKey.Y => nameof(NamedKey.Y), NamedKey.Z => nameof(NamedKey.Z),

		NamedKey.D1 => nameof(NamedKey.D1), NamedKey.D2 => nameof(NamedKey.D2), NamedKey.D3 => nameof(NamedKey.D3),
		NamedKey.D4 => nameof(NamedKey.D4), NamedKey.D5 => nameof(NamedKey.D5), NamedKey.D6 => nameof(NamedKey.D6),
		NamedKey.D7 => nameof(NamedKey.D7), NamedKey.D8 => nameof(NamedKey.D8), NamedKey.D9 => nameof(NamedKey.D9),
		NamedKey.D0 => nameof(NamedKey.D0),

		NamedKey.F1 => nameof(NamedKey.F1), NamedKey.F2 => nameof(NamedKey.F2), NamedKey.F3 => nameof(NamedKey.F3),
		NamedKey.F4 => nameof(NamedKey.F4), NamedKey.F5 => nameof(NamedKey.F5), NamedKey.F6 => nameof(NamedKey.F6),
		NamedKey.F7 => nameof(NamedKey.F7), NamedKey.F8 => nameof(NamedKey.F8), NamedKey.F9 => nameof(NamedKey.F9),
		NamedKey.F10 => nameof(NamedKey.F10), NamedKey.F11 => nameof(NamedKey.F11), NamedKey.F12 => nameof(NamedKey.F12),
		NamedKey.F13 => nameof(NamedKey.F13), NamedKey.F14 => nameof(NamedKey.F14), NamedKey.F15 => nameof(NamedKey.F15),
		NamedKey.F16 => nameof(NamedKey.F16), NamedKey.F17 => nameof(NamedKey.F17), NamedKey.F18 => nameof(NamedKey.F18),
		NamedKey.F19 => nameof(NamedKey.F19), NamedKey.F20 => nameof(NamedKey.F20), NamedKey.F21 => nameof(NamedKey.F21),
		NamedKey.F22 => nameof(NamedKey.F22), NamedKey.F23 => nameof(NamedKey.F23), NamedKey.F24 => nameof(NamedKey.F24),

		NamedKey.Keypad1 => nameof(NamedKey.Keypad1), NamedKey.Keypad2 => nameof(NamedKey.Keypad2),
		NamedKey.Keypad3 => nameof(NamedKey.Keypad3), NamedKey.Keypad4 => nameof(NamedKey.Keypad4),
		NamedKey.Keypad5 => nameof(NamedKey.Keypad5), NamedKey.Keypad6 => nameof(NamedKey.Keypad6),
		NamedKey.Keypad7 => nameof(NamedKey.Keypad7), NamedKey.Keypad8 => nameof(NamedKey.Keypad8),
		NamedKey.Keypad9 => nameof(NamedKey.Keypad9),

		NamedKey.Enter => nameof(NamedKey.Enter), NamedKey.Escape => nameof(NamedKey.Escape),
		NamedKey.Backspace => nameof(NamedKey.Backspace), NamedKey.Tab => nameof(NamedKey.Tab),
		NamedKey.Space => nameof(NamedKey.Space),

		NamedKey.Minus => nameof(NamedKey.Minus), NamedKey.Equals => nameof(NamedKey.Equals),
		NamedKey.LeftBracket => nameof(NamedKey.LeftBracket), NamedKey.RightBracket => nameof(NamedKey.RightBracket),
		NamedKey.Backslash => nameof(NamedKey.Backslash), NamedKey.Semicolon => nameof(NamedKey.Semicolon),
		NamedKey.Quote => nameof(NamedKey.Quote), NamedKey.Backtick => nameof(NamedKey.Backtick),
		NamedKey.Comma => nameof(NamedKey.Comma), NamedKey.Period => nameof(NamedKey.Period),
		NamedKey.Slash => nameof(NamedKey.Slash),

		NamedKey.CapsLock => nameof(NamedKey.CapsLock), NamedKey.PrintScreen => nameof(NamedKey.PrintScreen),
		NamedKey.ScrollLock => nameof(NamedKey.ScrollLock), NamedKey.Pause => nameof(NamedKey.Pause),
		NamedKey.Insert => nameof(NamedKey.Insert), NamedKey.Home => nameof(NamedKey.Home),
		NamedKey.PageUp => nameof(NamedKey.PageUp), NamedKey.Delete => nameof(NamedKey.Delete),
		NamedKey.End => nameof(NamedKey.End), NamedKey.PageDown => nameof(NamedKey.PageDown),
		NamedKey.ArrowRight => nameof(NamedKey.ArrowRight), NamedKey.ArrowLeft => nameof(NamedKey.ArrowLeft),
		NamedKey.ArrowDown => nameof(NamedKey.ArrowDown), NamedKey.ArrowUp => nameof(NamedKey.ArrowUp),
		NamedKey.Application => nameof(NamedKey.Application),

		NamedKey.NumLock => nameof(NamedKey.NumLock), NamedKey.KeypadDivide => nameof(NamedKey.KeypadDivide),
		NamedKey.KeypadMultiply => nameof(NamedKey.KeypadMultiply), NamedKey.KeypadMinus => nameof(NamedKey.KeypadMinus),
		NamedKey.KeypadPlus => nameof(NamedKey.KeypadPlus), NamedKey.KeypadEnter => nameof(NamedKey.KeypadEnter),
		NamedKey.Keypad0 => nameof(NamedKey.Keypad0), NamedKey.KeypadDecimal => nameof(NamedKey.KeypadDecimal),
		NamedKey.KeypadEqual => nameof(NamedKey.KeypadEqual),

		NamedKey.LeftControl => nameof(NamedKey.LeftControl), NamedKey.LeftShift => nameof(NamedKey.LeftShift),
		NamedKey.LeftAlt => nameof(NamedKey.LeftAlt), NamedKey.LeftGui => nameof(NamedKey.LeftGui),
		NamedKey.RightControl => nameof(NamedKey.RightControl), NamedKey.RightShift => nameof(NamedKey.RightShift),
		NamedKey.RightAlt => nameof(NamedKey.RightAlt), NamedKey.RightGui => nameof(NamedKey.RightGui),

		NamedKey.MediaPlayPause => nameof(NamedKey.MediaPlayPause),
		NamedKey.MediaNextTrack => nameof(NamedKey.MediaNextTrack),
		NamedKey.MediaPreviousTrack => nameof(NamedKey.MediaPreviousTrack),
		NamedKey.MediaStop => nameof(NamedKey.MediaStop), NamedKey.VolumeMute => nameof(NamedKey.VolumeMute),
		NamedKey.VolumeUp => nameof(NamedKey.VolumeUp), NamedKey.VolumeDown => nameof(NamedKey.VolumeDown),
		NamedKey.BrowserHome => nameof(NamedKey.BrowserHome), NamedKey.BrowserBack => nameof(NamedKey.BrowserBack),
		NamedKey.BrowserForward => nameof(NamedKey.BrowserForward), NamedKey.BrowserRefresh => nameof(NamedKey.BrowserRefresh),
		NamedKey.BrowserSearch => nameof(NamedKey.BrowserSearch),
		NamedKey.LaunchCalculator => nameof(NamedKey.LaunchCalculator), NamedKey.LaunchMail => nameof(NamedKey.LaunchMail),

		_ => null,
	};
}
