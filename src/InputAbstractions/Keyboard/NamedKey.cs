namespace SharpSticks.InputAbstractions.Keyboard;

/// <summary>
/// Recognizable keys with friendly names, each carrying its USB HID usage (see
/// the doc comment per member). Converts implicitly to <see cref="Key"/>. This is
/// a curated set, not exhaustive: layout-specific, IME/international, and
/// vendor-overloaded usages are intentionally absent — reach those through
/// <see cref="Key.FromKeyboard"/> / <see cref="Key.FromConsumer"/> so a key whose
/// meaning isn't stable never gets a single misleading name. Values pack the HID
/// page so keyboard (0x07) and consumer (0x0C) members can't collide.
/// </summary>
public enum NamedKey
{
	// ── Letters (HID keyboard page 0x07) ──────────────────────────────────
	/// <summary>HID 0x07/0x04 — the <c>A</c> key.</summary>
	A = Key.KeyboardBase | 0x04,
	/// <summary>HID 0x07/0x05 — the <c>B</c> key.</summary>
	B = Key.KeyboardBase | 0x05,
	/// <summary>HID 0x07/0x06 — the <c>C</c> key.</summary>
	C = Key.KeyboardBase | 0x06,
	/// <summary>HID 0x07/0x07 — the <c>D</c> key.</summary>
	D = Key.KeyboardBase | 0x07,
	/// <summary>HID 0x07/0x08 — the <c>E</c> key.</summary>
	E = Key.KeyboardBase | 0x08,
	/// <summary>HID 0x07/0x09 — the <c>F</c> key.</summary>
	F = Key.KeyboardBase | 0x09,
	/// <summary>HID 0x07/0x0A — the <c>G</c> key.</summary>
	G = Key.KeyboardBase | 0x0A,
	/// <summary>HID 0x07/0x0B — the <c>H</c> key.</summary>
	H = Key.KeyboardBase | 0x0B,
	/// <summary>HID 0x07/0x0C — the <c>I</c> key.</summary>
	I = Key.KeyboardBase | 0x0C,
	/// <summary>HID 0x07/0x0D — the <c>J</c> key.</summary>
	J = Key.KeyboardBase | 0x0D,
	/// <summary>HID 0x07/0x0E — the <c>K</c> key.</summary>
	K = Key.KeyboardBase | 0x0E,
	/// <summary>HID 0x07/0x0F — the <c>L</c> key.</summary>
	L = Key.KeyboardBase | 0x0F,
	/// <summary>HID 0x07/0x10 — the <c>M</c> key.</summary>
	M = Key.KeyboardBase | 0x10,
	/// <summary>HID 0x07/0x11 — the <c>N</c> key.</summary>
	N = Key.KeyboardBase | 0x11,
	/// <summary>HID 0x07/0x12 — the <c>O</c> key.</summary>
	O = Key.KeyboardBase | 0x12,
	/// <summary>HID 0x07/0x13 — the <c>P</c> key.</summary>
	P = Key.KeyboardBase | 0x13,
	/// <summary>HID 0x07/0x14 — the <c>Q</c> key.</summary>
	Q = Key.KeyboardBase | 0x14,
	/// <summary>HID 0x07/0x15 — the <c>R</c> key.</summary>
	R = Key.KeyboardBase | 0x15,
	/// <summary>HID 0x07/0x16 — the <c>S</c> key.</summary>
	S = Key.KeyboardBase | 0x16,
	/// <summary>HID 0x07/0x17 — the <c>T</c> key.</summary>
	T = Key.KeyboardBase | 0x17,
	/// <summary>HID 0x07/0x18 — the <c>U</c> key.</summary>
	U = Key.KeyboardBase | 0x18,
	/// <summary>HID 0x07/0x19 — the <c>V</c> key.</summary>
	V = Key.KeyboardBase | 0x19,
	/// <summary>HID 0x07/0x1A — the <c>W</c> key.</summary>
	W = Key.KeyboardBase | 0x1A,
	/// <summary>HID 0x07/0x1B — the <c>X</c> key.</summary>
	X = Key.KeyboardBase | 0x1B,
	/// <summary>HID 0x07/0x1C — the <c>Y</c> key.</summary>
	Y = Key.KeyboardBase | 0x1C,
	/// <summary>HID 0x07/0x1D — the <c>Z</c> key.</summary>
	Z = Key.KeyboardBase | 0x1D,

	// ── Digits (top row) ──────────────────────────────────────────────────
	/// <summary>HID 0x07/0x1E — the <c>1 !</c> key.</summary>
	D1 = Key.KeyboardBase | 0x1E,
	/// <summary>HID 0x07/0x1F — the <c>2 @</c> key.</summary>
	D2 = Key.KeyboardBase | 0x1F,
	/// <summary>HID 0x07/0x20 — the <c>3 #</c> key.</summary>
	D3 = Key.KeyboardBase | 0x20,
	/// <summary>HID 0x07/0x21 — the <c>4 $</c> key.</summary>
	D4 = Key.KeyboardBase | 0x21,
	/// <summary>HID 0x07/0x22 — the <c>5 %</c> key.</summary>
	D5 = Key.KeyboardBase | 0x22,
	/// <summary>HID 0x07/0x23 — the <c>6 ^</c> key.</summary>
	D6 = Key.KeyboardBase | 0x23,
	/// <summary>HID 0x07/0x24 — the <c>7 &amp;</c> key.</summary>
	D7 = Key.KeyboardBase | 0x24,
	/// <summary>HID 0x07/0x25 — the <c>8 *</c> key.</summary>
	D8 = Key.KeyboardBase | 0x25,
	/// <summary>HID 0x07/0x26 — the <c>9 (</c> key.</summary>
	D9 = Key.KeyboardBase | 0x26,
	/// <summary>HID 0x07/0x27 — the <c>0 )</c> key.</summary>
	D0 = Key.KeyboardBase | 0x27,

	// ── Whitespace / editing ──────────────────────────────────────────────
	/// <summary>HID 0x07/0x28 — <c>Enter</c> / Return.</summary>
	Enter = Key.KeyboardBase | 0x28,
	/// <summary>HID 0x07/0x29 — <c>Escape</c>.</summary>
	Escape = Key.KeyboardBase | 0x29,
	/// <summary>HID 0x07/0x2A — <c>Backspace</c>.</summary>
	Backspace = Key.KeyboardBase | 0x2A,
	/// <summary>HID 0x07/0x2B — <c>Tab</c>.</summary>
	Tab = Key.KeyboardBase | 0x2B,
	/// <summary>HID 0x07/0x2C — <c>Space</c>.</summary>
	Space = Key.KeyboardBase | 0x2C,

	// ── Punctuation ───────────────────────────────────────────────────────
	/// <summary>HID 0x07/0x2D — the <c>- _</c> key.</summary>
	Minus = Key.KeyboardBase | 0x2D,
	/// <summary>HID 0x07/0x2E — the <c>= +</c> key.</summary>
	Equals = Key.KeyboardBase | 0x2E,
	/// <summary>HID 0x07/0x2F — the <c>[ {</c> key.</summary>
	LeftBracket = Key.KeyboardBase | 0x2F,
	/// <summary>HID 0x07/0x30 — the <c>] }</c> key.</summary>
	RightBracket = Key.KeyboardBase | 0x30,
	/// <summary>HID 0x07/0x31 — the <c>\ |</c> key.</summary>
	Backslash = Key.KeyboardBase | 0x31,
	/// <summary>HID 0x07/0x33 — the <c>; :</c> key.</summary>
	Semicolon = Key.KeyboardBase | 0x33,
	/// <summary>HID 0x07/0x34 — the <c>' "</c> key.</summary>
	Quote = Key.KeyboardBase | 0x34,
	/// <summary>HID 0x07/0x35 — the <c>` ~</c> (grave) key.</summary>
	Backtick = Key.KeyboardBase | 0x35,
	/// <summary>HID 0x07/0x36 — the <c>, &lt;</c> key.</summary>
	Comma = Key.KeyboardBase | 0x36,
	/// <summary>HID 0x07/0x37 — the <c>. &gt;</c> key.</summary>
	Period = Key.KeyboardBase | 0x37,
	/// <summary>HID 0x07/0x38 — the <c>/ ?</c> key.</summary>
	Slash = Key.KeyboardBase | 0x38,

	// ── Function keys ─────────────────────────────────────────────────────
	/// <summary>HID 0x07/0x3A — <c>F1</c>.</summary>
	F1 = Key.KeyboardBase | 0x3A,
	/// <summary>HID 0x07/0x3B — <c>F2</c>.</summary>
	F2 = Key.KeyboardBase | 0x3B,
	/// <summary>HID 0x07/0x3C — <c>F3</c>.</summary>
	F3 = Key.KeyboardBase | 0x3C,
	/// <summary>HID 0x07/0x3D — <c>F4</c>.</summary>
	F4 = Key.KeyboardBase | 0x3D,
	/// <summary>HID 0x07/0x3E — <c>F5</c>.</summary>
	F5 = Key.KeyboardBase | 0x3E,
	/// <summary>HID 0x07/0x3F — <c>F6</c>.</summary>
	F6 = Key.KeyboardBase | 0x3F,
	/// <summary>HID 0x07/0x40 — <c>F7</c>.</summary>
	F7 = Key.KeyboardBase | 0x40,
	/// <summary>HID 0x07/0x41 — <c>F8</c>.</summary>
	F8 = Key.KeyboardBase | 0x41,
	/// <summary>HID 0x07/0x42 — <c>F9</c>.</summary>
	F9 = Key.KeyboardBase | 0x42,
	/// <summary>HID 0x07/0x43 — <c>F10</c>.</summary>
	F10 = Key.KeyboardBase | 0x43,
	/// <summary>HID 0x07/0x44 — <c>F11</c>.</summary>
	F11 = Key.KeyboardBase | 0x44,
	/// <summary>HID 0x07/0x45 — <c>F12</c>.</summary>
	F12 = Key.KeyboardBase | 0x45,
	/// <summary>HID 0x07/0x68 — <c>F13</c>.</summary>
	F13 = Key.KeyboardBase | 0x68,
	/// <summary>HID 0x07/0x69 — <c>F14</c>.</summary>
	F14 = Key.KeyboardBase | 0x69,
	/// <summary>HID 0x07/0x6A — <c>F15</c>.</summary>
	F15 = Key.KeyboardBase | 0x6A,
	/// <summary>HID 0x07/0x6B — <c>F16</c>.</summary>
	F16 = Key.KeyboardBase | 0x6B,
	/// <summary>HID 0x07/0x6C — <c>F17</c>.</summary>
	F17 = Key.KeyboardBase | 0x6C,
	/// <summary>HID 0x07/0x6D — <c>F18</c>.</summary>
	F18 = Key.KeyboardBase | 0x6D,
	/// <summary>HID 0x07/0x6E — <c>F19</c>.</summary>
	F19 = Key.KeyboardBase | 0x6E,
	/// <summary>HID 0x07/0x6F — <c>F20</c>.</summary>
	F20 = Key.KeyboardBase | 0x6F,
	/// <summary>HID 0x07/0x70 — <c>F21</c>.</summary>
	F21 = Key.KeyboardBase | 0x70,
	/// <summary>HID 0x07/0x71 — <c>F22</c>.</summary>
	F22 = Key.KeyboardBase | 0x71,
	/// <summary>HID 0x07/0x72 — <c>F23</c>.</summary>
	F23 = Key.KeyboardBase | 0x72,
	/// <summary>HID 0x07/0x73 — <c>F24</c>.</summary>
	F24 = Key.KeyboardBase | 0x73,

	// ── Navigation / system ───────────────────────────────────────────────
	/// <summary>HID 0x07/0x39 — <c>Caps Lock</c>.</summary>
	CapsLock = Key.KeyboardBase | 0x39,
	/// <summary>HID 0x07/0x46 — <c>Print Screen</c>.</summary>
	PrintScreen = Key.KeyboardBase | 0x46,
	/// <summary>HID 0x07/0x47 — <c>Scroll Lock</c>.</summary>
	ScrollLock = Key.KeyboardBase | 0x47,
	/// <summary>HID 0x07/0x48 — <c>Pause</c> / Break.</summary>
	Pause = Key.KeyboardBase | 0x48,
	/// <summary>HID 0x07/0x49 — <c>Insert</c>.</summary>
	Insert = Key.KeyboardBase | 0x49,
	/// <summary>HID 0x07/0x4A — <c>Home</c>.</summary>
	Home = Key.KeyboardBase | 0x4A,
	/// <summary>HID 0x07/0x4B — <c>Page Up</c>.</summary>
	PageUp = Key.KeyboardBase | 0x4B,
	/// <summary>HID 0x07/0x4C — <c>Delete</c> (forward delete).</summary>
	Delete = Key.KeyboardBase | 0x4C,
	/// <summary>HID 0x07/0x4D — <c>End</c>.</summary>
	End = Key.KeyboardBase | 0x4D,
	/// <summary>HID 0x07/0x4E — <c>Page Down</c>.</summary>
	PageDown = Key.KeyboardBase | 0x4E,
	/// <summary>HID 0x07/0x4F — <c>→</c> Right arrow.</summary>
	ArrowRight = Key.KeyboardBase | 0x4F,
	/// <summary>HID 0x07/0x50 — <c>←</c> Left arrow.</summary>
	ArrowLeft = Key.KeyboardBase | 0x50,
	/// <summary>HID 0x07/0x51 — <c>↓</c> Down arrow.</summary>
	ArrowDown = Key.KeyboardBase | 0x51,
	/// <summary>HID 0x07/0x52 — <c>↑</c> Up arrow.</summary>
	ArrowUp = Key.KeyboardBase | 0x52,
	/// <summary>HID 0x07/0x65 — <c>≣ Menu</c> / Application key.</summary>
	Application = Key.KeyboardBase | 0x65,

	// ── Keypad ────────────────────────────────────────────────────────────
	/// <summary>HID 0x07/0x53 — keypad <c>Num Lock</c>.</summary>
	NumLock = Key.KeyboardBase | 0x53,
	/// <summary>HID 0x07/0x54 — keypad <c>/</c>.</summary>
	KeypadDivide = Key.KeyboardBase | 0x54,
	/// <summary>HID 0x07/0x55 — keypad <c>*</c>.</summary>
	KeypadMultiply = Key.KeyboardBase | 0x55,
	/// <summary>HID 0x07/0x56 — keypad <c>-</c>.</summary>
	KeypadMinus = Key.KeyboardBase | 0x56,
	/// <summary>HID 0x07/0x57 — keypad <c>+</c>.</summary>
	KeypadPlus = Key.KeyboardBase | 0x57,
	/// <summary>HID 0x07/0x58 — keypad <c>Enter</c>.</summary>
	KeypadEnter = Key.KeyboardBase | 0x58,
	/// <summary>HID 0x07/0x59 — keypad <c>1</c> / End.</summary>
	Keypad1 = Key.KeyboardBase | 0x59,
	/// <summary>HID 0x07/0x5A — keypad <c>2</c> / Down.</summary>
	Keypad2 = Key.KeyboardBase | 0x5A,
	/// <summary>HID 0x07/0x5B — keypad <c>3</c> / Page Down.</summary>
	Keypad3 = Key.KeyboardBase | 0x5B,
	/// <summary>HID 0x07/0x5C — keypad <c>4</c> / Left.</summary>
	Keypad4 = Key.KeyboardBase | 0x5C,
	/// <summary>HID 0x07/0x5D — keypad <c>5</c>.</summary>
	Keypad5 = Key.KeyboardBase | 0x5D,
	/// <summary>HID 0x07/0x5E — keypad <c>6</c> / Right.</summary>
	Keypad6 = Key.KeyboardBase | 0x5E,
	/// <summary>HID 0x07/0x5F — keypad <c>7</c> / Home.</summary>
	Keypad7 = Key.KeyboardBase | 0x5F,
	/// <summary>HID 0x07/0x60 — keypad <c>8</c> / Up.</summary>
	Keypad8 = Key.KeyboardBase | 0x60,
	/// <summary>HID 0x07/0x61 — keypad <c>9</c> / Page Up.</summary>
	Keypad9 = Key.KeyboardBase | 0x61,
	/// <summary>HID 0x07/0x62 — keypad <c>0</c> / Insert.</summary>
	Keypad0 = Key.KeyboardBase | 0x62,
	/// <summary>HID 0x07/0x63 — keypad <c>.</c> / Delete.</summary>
	KeypadDecimal = Key.KeyboardBase | 0x63,
	/// <summary>HID 0x07/0x67 — keypad <c>=</c>.</summary>
	KeypadEqual = Key.KeyboardBase | 0x67,

	// ── Modifiers ─────────────────────────────────────────────────────────
	/// <summary>HID 0x07/0xE0 — left <c>Ctrl</c>.</summary>
	LeftControl = Key.KeyboardBase | 0xE0,
	/// <summary>HID 0x07/0xE1 — left <c>Shift</c>.</summary>
	LeftShift = Key.KeyboardBase | 0xE1,
	/// <summary>HID 0x07/0xE2 — left <c>Alt</c>.</summary>
	LeftAlt = Key.KeyboardBase | 0xE2,
	/// <summary>HID 0x07/0xE3 — left <c>GUI</c> (Windows / Command / Meta).</summary>
	LeftGui = Key.KeyboardBase | 0xE3,
	/// <summary>HID 0x07/0xE4 — right <c>Ctrl</c>.</summary>
	RightControl = Key.KeyboardBase | 0xE4,
	/// <summary>HID 0x07/0xE5 — right <c>Shift</c>.</summary>
	RightShift = Key.KeyboardBase | 0xE5,
	/// <summary>HID 0x07/0xE6 — right <c>Alt</c> / AltGr.</summary>
	RightAlt = Key.KeyboardBase | 0xE6,
	/// <summary>HID 0x07/0xE7 — right <c>GUI</c> (Windows / Command / Meta).</summary>
	RightGui = Key.KeyboardBase | 0xE7,

	// ── Consumer page (0x0C): media / browser / launch ────────────────────
	/// <summary>HID 0x0C/0x00CD — media <c>Play / Pause</c>.</summary>
	MediaPlayPause = Key.ConsumerBase | 0x00CD,
	/// <summary>HID 0x0C/0x00B5 — media <c>Next Track</c>.</summary>
	MediaNextTrack = Key.ConsumerBase | 0x00B5,
	/// <summary>HID 0x0C/0x00B6 — media <c>Previous Track</c>.</summary>
	MediaPreviousTrack = Key.ConsumerBase | 0x00B6,
	/// <summary>HID 0x0C/0x00B7 — media <c>Stop</c>.</summary>
	MediaStop = Key.ConsumerBase | 0x00B7,
	/// <summary>HID 0x0C/0x00E2 — <c>Mute</c>.</summary>
	VolumeMute = Key.ConsumerBase | 0x00E2,
	/// <summary>HID 0x0C/0x00E9 — <c>Volume Up</c>.</summary>
	VolumeUp = Key.ConsumerBase | 0x00E9,
	/// <summary>HID 0x0C/0x00EA — <c>Volume Down</c>.</summary>
	VolumeDown = Key.ConsumerBase | 0x00EA,
	/// <summary>HID 0x0C/0x0223 — browser <c>Home</c>.</summary>
	BrowserHome = Key.ConsumerBase | 0x0223,
	/// <summary>HID 0x0C/0x0224 — browser <c>Back</c>.</summary>
	BrowserBack = Key.ConsumerBase | 0x0224,
	/// <summary>HID 0x0C/0x0225 — browser <c>Forward</c>.</summary>
	BrowserForward = Key.ConsumerBase | 0x0225,
	/// <summary>HID 0x0C/0x0227 — browser <c>Refresh</c>.</summary>
	BrowserRefresh = Key.ConsumerBase | 0x0227,
	/// <summary>HID 0x0C/0x0221 — browser <c>Search</c>.</summary>
	BrowserSearch = Key.ConsumerBase | 0x0221,
	/// <summary>HID 0x0C/0x0192 — launch <c>Calculator</c>.</summary>
	LaunchCalculator = Key.ConsumerBase | 0x0192,
	/// <summary>HID 0x0C/0x018A — launch <c>Mail</c>.</summary>
	LaunchMail = Key.ConsumerBase | 0x018A,
}
