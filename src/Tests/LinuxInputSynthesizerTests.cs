using SharpSticks.InputSynthesis.Linux;
using SharpSticks.LinuxNative;

namespace SharpSticks.Tests;

/// <summary>
/// Validates the Linux synthesizer's translation without touching uinput: a
/// capturing send delegate records the <c>input_event</c>s that would be written,
/// so the HID→evdev keycode tables, the down/up value, page dispatch, mouse
/// <c>BTN_*</c> codes, and the SYN-on-flush behavior are all asserted
/// deterministically (and cross-platform — no real device is opened).
/// </summary>
public sealed class LinuxInputSynthesizerTests
{
	private readonly List<LinuxInputEvent> _Sent = [];

	private sealed class TestLinuxInputEventSender : ILinuxInputEventSender
	{
		private readonly List<LinuxInputEvent> _List;

		public TestLinuxInputEventSender(List<LinuxInputEvent> list)
		{
			_List = list;
		}

		public void Initialize()
		{
		}

		public void Write(LinuxInputEvent ev) => _List.Add(ev);
	}

	private LinuxInputSynthesizer NewSynth() => new(
		new TestLinuxInputEventSender(_Sent),
		false);

	private LinuxInputEvent Single()
	{
		Assert.Single(_Sent);
		return _Sent[0];
	}

	[Fact]
	public void KeyDown_Letter_EmitsEvKeyWithValue1()
	{
		NewSynth().KeyDown(NamedKey.A);

		var e = Single();
		Assert.Equal(EvType.Key, e.Type);
		Assert.Equal((ushort)30, e.Code); // KEY_A
		Assert.Equal(1, e.Value);
	}

	[Fact]
	public void KeyUp_Letter_EmitsValue0()
	{
		NewSynth().KeyUp(NamedKey.A);

		var e = Single();
		Assert.Equal(EvType.Key, e.Type);
		Assert.Equal((ushort)30, e.Code);
		Assert.Equal(0, e.Value);
	}

	[Fact]
	public void KeyDown_NavigationKey_MapsToEvdevCode()
	{
		NewSynth().KeyDown(NamedKey.ArrowUp);
		Assert.Equal((ushort)103, Single().Code); // KEY_UP
	}

	[Fact]
	public void KeyDown_ConsumerKey_MapsToEvdevMediaCode()
	{
		NewSynth().KeyDown(NamedKey.MediaPlayPause);

		var e = Single();
		Assert.Equal(EvType.Key, e.Type);
		Assert.Equal((ushort)164, e.Code); // KEY_PLAYPAUSE
	}

	[Fact]
	public void MouseButtons_MapToBtnCodes()
	{
		var synth = NewSynth();
		synth.MouseButtonDown(OutputMouseButton.Left);
		synth.MouseButtonDown(OutputMouseButton.X1);

		Assert.Equal((ushort)0x110, _Sent[0].Code); // BTN_LEFT
		Assert.Equal(1, _Sent[0].Value);
		Assert.Equal((ushort)0x113, _Sent[1].Code); // BTN_SIDE
	}

	[Fact]
	public void Flush_AfterEvents_EmitsSynReport()
	{
		var synth = NewSynth();
		synth.KeyDown(NamedKey.A);
		synth.Flush();

		Assert.Equal(2, _Sent.Count);
		Assert.Equal(EvType.Syn, _Sent[1].Type);
		Assert.Equal(LinuxEventCodes.SynReport, _Sent[1].Code);
	}

	[Fact]
	public void Flush_WithNothingPending_EmitsNothing()
	{
		NewSynth().Flush();
		Assert.Empty(_Sent);
	}

	[Fact]
	public void KeyDown_UnmappedKeyboardUsage_Throws_AndSendsNothing()
	{
		Assert.Throws<NotSupportedException>(() => NewSynth().KeyDown(Key.FromKeyboard(0x87)));
		Assert.Empty(_Sent);
	}

	[Fact]
	public void KeyDown_UnsupportedUsagePage_Throws()
	{
		Assert.Throws<NotSupportedException>(() => NewSynth().KeyDown(new Key((0x99 << 16) | 0x01)));
		Assert.Empty(_Sent);
	}
}
