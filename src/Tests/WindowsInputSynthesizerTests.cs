using SharpSticks.InputSynthesis.Windows;

namespace SharpSticks.Tests;

/// <summary>
/// Validates the Windows synthesizer's translation without touching the OS: a
/// capturing send delegate records the <c>INPUT</c> structs that <c>SendInput</c>
/// would receive, so the scancode/virtual-key tables, the down/up and extended
/// flags, page dispatch, and mouse-flag mapping are all asserted deterministically
/// (and cross-platform — no real injection).
/// </summary>
public sealed class WindowsInputSynthesizerTests
{
	private readonly List<Win32Input.Input> _Sent = [];

	private WindowsInputSynthesizer NewSynth() => new(_Sent.Add);

	private Win32Input.Input Single()
	{
		Assert.Single(_Sent);
		return _Sent[0];
	}

	[Fact]
	public void KeyDown_Letter_EmitsScancode_NoExtraFlags()
	{
		NewSynth().KeyDown(NamedKey.A);

		var i = Single();
		Assert.Equal(Win32Input.InputKeyboard, i.Type);
		Assert.Equal((ushort)0x1E, i.Union.Keyboard.Scan);
		Assert.Equal((ushort)0, i.Union.Keyboard.Vk);
		Assert.Equal(Win32Input.KeyEventScancode, i.Union.Keyboard.Flags);
	}

	[Fact]
	public void KeyUp_Letter_SetsKeyUpFlag()
	{
		NewSynth().KeyUp(NamedKey.A);

		var i = Single();
		Assert.Equal(Win32Input.KeyEventScancode | Win32Input.KeyEventKeyUp, i.Union.Keyboard.Flags);
	}

	[Fact]
	public void KeyDown_NavigationKey_SetsExtendedFlag()
	{
		NewSynth().KeyDown(NamedKey.ArrowUp);

		var i = Single();
		Assert.Equal((ushort)0x48, i.Union.Keyboard.Scan);
		Assert.Equal(Win32Input.KeyEventScancode | Win32Input.KeyEventExtendedKey, i.Union.Keyboard.Flags);
	}

	[Fact]
	public void KeyDown_ConsumerKey_EmitsVirtualKey_NotScancode()
	{
		NewSynth().KeyDown(NamedKey.MediaPlayPause);

		var i = Single();
		Assert.Equal(Win32Input.InputKeyboard, i.Type);
		Assert.Equal((ushort)0xB3, i.Union.Keyboard.Vk); // VK_MEDIA_PLAY_PAUSE
		Assert.Equal((ushort)0, i.Union.Keyboard.Scan);
		Assert.Equal(Win32Input.KeyEventExtendedKey, i.Union.Keyboard.Flags);
	}

	[Fact]
	public void MouseButtonDown_Left_EmitsLeftDownFlag()
	{
		NewSynth().MouseButtonDown(OutputMouseButton.Left);

		var i = Single();
		Assert.Equal(Win32Input.InputMouse, i.Type);
		Assert.Equal(Win32Input.MouseEventLeftDown, i.Union.Mouse.Flags);
	}

	[Fact]
	public void MouseButton_X1_SetsXButtonData()
	{
		var synth = NewSynth();
		synth.MouseButtonDown(OutputMouseButton.X1);
		synth.MouseButtonUp(OutputMouseButton.X1);

		Assert.Equal(2, _Sent.Count);
		Assert.Equal(Win32Input.MouseEventXDown, _Sent[0].Union.Mouse.Flags);
		Assert.Equal(Win32Input.XButton1, _Sent[0].Union.Mouse.MouseData);
		Assert.Equal(Win32Input.MouseEventXUp, _Sent[1].Union.Mouse.Flags);
		Assert.Equal(Win32Input.XButton1, _Sent[1].Union.Mouse.MouseData);
	}

	[Fact]
	public void KeyDown_UnmappedKeyboardUsage_Throws_AndSendsNothing()
	{
		// 0x87 (International1) is intentionally absent from the scancode table.
		Assert.Throws<NotSupportedException>(() => NewSynth().KeyDown(Key.FromKeyboard(0x87)));
		Assert.Empty(_Sent);
	}

	[Fact]
	public void KeyDown_UnsupportedUsagePage_Throws()
	{
		var key = new Key((0x99 << 16) | 0x01);
		Assert.Throws<NotSupportedException>(() => NewSynth().KeyDown(key));
		Assert.Empty(_Sent);
	}
}
