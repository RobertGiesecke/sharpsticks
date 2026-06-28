using SharpSticks.InputSynthesis.Keyboard;

namespace SharpSticks.InputSynthesis.Linux;

/// <summary>
/// Maps HID consumer-page (0x0C) usages — media transport, volume, browser,
/// launch — to Linux evdev <c>KEY_*</c> codes (evdev exposes these as ordinary
/// key codes). Pure — does not write. Unmapped usages yield <c>null</c>.
/// </summary>
internal static class EvdevConsumerKeyEmitter
{
	public static LinuxInputEvent? TryBuild(Key key, bool down) =>
		TryGetKeyCode(key.Usage) is { } code ? EvdevEvent.Key(code, down) : null;

	// HID consumer usage (0x0C page) -> evdev KEY_* code.
	private static ushort? TryGetKeyCode(int usage) => usage switch
	{
		0x00CD => 164, // Play/Pause   -> KEY_PLAYPAUSE
		0x00B5 => 163, // Next Track   -> KEY_NEXTSONG
		0x00B6 => 165, // Prev Track   -> KEY_PREVIOUSSONG
		0x00B7 => 166, // Stop         -> KEY_STOPCD
		0x00E2 => 113, // Mute         -> KEY_MUTE
		0x00E9 => 115, // Volume Up    -> KEY_VOLUMEUP
		0x00EA => 114, // Volume Down  -> KEY_VOLUMEDOWN
		0x0223 => 172, // Browser Home -> KEY_HOMEPAGE
		0x0224 => 158, // Browser Back -> KEY_BACK
		0x0225 => 159, // Browser Fwd  -> KEY_FORWARD
		0x0227 => 173, // Browser Refresh -> KEY_REFRESH
		0x0221 => 217, // Browser Search  -> KEY_SEARCH
		0x0192 => 140, // Calculator   -> KEY_CALC
		0x018A => 155, // Mail         -> KEY_MAIL
		_ => null,
	};
}
