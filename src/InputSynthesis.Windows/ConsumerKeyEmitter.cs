using System.Collections.Frozen;
using SharpSticks.InputAbstractions.Keyboard;

namespace SharpSticks.InputSynthesis.Windows;

/// <summary>
/// Emits HID consumer-page (0x0C) keys — media transport, volume, browser,
/// launch — as Windows virtual-key events via <c>SendInput</c>. These usages have
/// no PS/2 scancode; Windows exposes them as <c>VK_MEDIA_*</c> / <c>VK_VOLUME_*</c>
/// / <c>VK_BROWSER_*</c> / <c>VK_LAUNCH_*</c>, which SendInput accepts by virtual
/// key. Unmapped consumer usages throw <see cref="NotSupportedException"/>.
/// </summary>
internal static class ConsumerKeyEmitter
{
	// HID consumer usage (0x0C page) -> Windows virtual-key code.
	private static readonly FrozenDictionary<int, ushort> Map =
		new Dictionary<int, ushort>
		{
			[0x00CD] = 0xB3, // Play/Pause   -> VK_MEDIA_PLAY_PAUSE
			[0x00B5] = 0xB0, // Next Track   -> VK_MEDIA_NEXT_TRACK
			[0x00B6] = 0xB1, // Prev Track   -> VK_MEDIA_PREV_TRACK
			[0x00B7] = 0xB2, // Stop         -> VK_MEDIA_STOP
			[0x00E2] = 0xAD, // Mute         -> VK_VOLUME_MUTE
			[0x00E9] = 0xAF, // Volume Up    -> VK_VOLUME_UP
			[0x00EA] = 0xAE, // Volume Down  -> VK_VOLUME_DOWN
			[0x0223] = 0xAC, // Browser Home -> VK_BROWSER_HOME
			[0x0224] = 0xA6, // Browser Back -> VK_BROWSER_BACK
			[0x0225] = 0xA7, // Browser Fwd  -> VK_BROWSER_FORWARD
			[0x0227] = 0xA8, // Browser Refresh -> VK_BROWSER_REFRESH
			[0x0221] = 0xAA, // Browser Search  -> VK_BROWSER_SEARCH
			[0x0192] = 0xB7, // Calculator   -> VK_LAUNCH_APP2
			[0x018A] = 0xB4, // Mail         -> VK_LAUNCH_MAIL
		}.ToFrozenDictionary();

	public static bool CanEmit(int usage) => Map.ContainsKey(usage);

	public static void Emit(Key key, bool down)
	{
		if (!Map.TryGetValue(key.Usage, out var vk))
		{
			throw new NotSupportedException(
				$"No virtual-key mapping for consumer HID usage 0x{key.Usage:X} ({key}).");
		}

		Win32Input.Send(Win32Input.KeyByVirtualKey(vk, up: !down));
	}
}
