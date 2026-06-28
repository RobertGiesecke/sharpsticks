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
		((NamedKey)Code).TryGetEnumName() ?? $"Key(0x{Code:X})";

}
