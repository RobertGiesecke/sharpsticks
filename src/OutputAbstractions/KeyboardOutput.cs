using SharpSticks.InputSynthesis.Keyboard;

namespace SharpSticks.OutputAbstractions;

public static class KeyboardOutput
{

	/// <inheritdoc cref="Key.FromConsumer"/>
	public static Key FromConsumerKeyCode(int usage) => Key.FromConsumer(usage);

	/// <inheritdoc cref="Key.FromKeyboard"/>
	public static Key FromKeyboardCode(int usage) => Key.FromKeyboard(usage);

	public static Key FromNamedKey(NamedKey key) => Enum.IsDefinedFast(key)
		? key
		: throw new ArgumentException($"Key {key} is not a named key.", nameof(key));

	public static class NamedKeys
	{
		public static class Letters
		{
			/// <inheritdoc cref="NamedKey.A"/>
			public static readonly KeyTarget A = new (NamedKey.A);

			/// <inheritdoc cref="NamedKey.B"/>
			public static readonly KeyTarget B = new (NamedKey.B);

			/// <inheritdoc cref="NamedKey.C"/>
			public static readonly KeyTarget C = new (NamedKey.C);

			/// <inheritdoc cref="NamedKey.D"/>
			public static readonly KeyTarget D = new (NamedKey.D);

			/// <inheritdoc cref="NamedKey.E"/>
			public static readonly KeyTarget E = new (NamedKey.E);

			/// <inheritdoc cref="NamedKey.F"/>
			public static readonly KeyTarget F = new (NamedKey.F);

			/// <inheritdoc cref="NamedKey.G"/>
			public static readonly KeyTarget G = new (NamedKey.G);

			/// <inheritdoc cref="NamedKey.H"/>
			public static readonly KeyTarget H = new (NamedKey.H);

			/// <inheritdoc cref="NamedKey.I"/>
			public static readonly KeyTarget I = new (NamedKey.I);

			/// <inheritdoc cref="NamedKey.J"/>
			public static readonly KeyTarget J = new (NamedKey.J);

			/// <inheritdoc cref="NamedKey.K"/>
			public static readonly KeyTarget K = new (NamedKey.K);

			/// <inheritdoc cref="NamedKey.L"/>
			public static readonly KeyTarget L = new (NamedKey.L);

			/// <inheritdoc cref="NamedKey.M"/>
			public static readonly KeyTarget M = new (NamedKey.M);

			/// <inheritdoc cref="NamedKey.N"/>
			public static readonly KeyTarget N = new (NamedKey.N);

			/// <inheritdoc cref="NamedKey.O"/>
			public static readonly KeyTarget O = new (NamedKey.O);

			/// <inheritdoc cref="NamedKey.P"/>
			public static readonly KeyTarget P = new (NamedKey.P);

			/// <inheritdoc cref="NamedKey.Q"/>
			public static readonly KeyTarget Q = new (NamedKey.Q);

			/// <inheritdoc cref="NamedKey.R"/>
			public static readonly KeyTarget R = new (NamedKey.R);

			/// <inheritdoc cref="NamedKey.S"/>
			public static readonly KeyTarget S = new (NamedKey.S);

			/// <inheritdoc cref="NamedKey.T"/>
			public static readonly KeyTarget T = new (NamedKey.T);

			/// <inheritdoc cref="NamedKey.U"/>
			public static readonly KeyTarget U = new (NamedKey.U);

			/// <inheritdoc cref="NamedKey.V"/>
			public static readonly KeyTarget V = new (NamedKey.V);

			/// <inheritdoc cref="NamedKey.W"/>
			public static readonly KeyTarget W = new (NamedKey.W);

			/// <inheritdoc cref="NamedKey.X"/>
			public static readonly KeyTarget X = new (NamedKey.X);

			/// <inheritdoc cref="NamedKey.Y"/>
			public static readonly KeyTarget Y = new (NamedKey.Y);

			/// <inheritdoc cref="NamedKey.Z"/>
			public static readonly KeyTarget Z = new (NamedKey.Z);
		}

		public static class Digits
		{
			/// <inheritdoc cref="NamedKey.D1"/>
			public static readonly KeyTarget D1 = new (NamedKey.D1);

			/// <inheritdoc cref="NamedKey.D2"/>
			public static readonly KeyTarget D2 = new (NamedKey.D2);

			/// <inheritdoc cref="NamedKey.D3"/>
			public static readonly KeyTarget D3 = new (NamedKey.D3);

			/// <inheritdoc cref="NamedKey.D4"/>
			public static readonly KeyTarget D4 = new (NamedKey.D4);

			/// <inheritdoc cref="NamedKey.D5"/>
			public static readonly KeyTarget D5 = new (NamedKey.D5);

			/// <inheritdoc cref="NamedKey.D6"/>
			public static readonly KeyTarget D6 = new (NamedKey.D6);

			/// <inheritdoc cref="NamedKey.D7"/>
			public static readonly KeyTarget D7 = new (NamedKey.D7);

			/// <inheritdoc cref="NamedKey.D8"/>
			public static readonly KeyTarget D8 = new (NamedKey.D8);

			/// <inheritdoc cref="NamedKey.D9"/>
			public static readonly KeyTarget D9 = new (NamedKey.D9);

			/// <inheritdoc cref="NamedKey.D0"/>
			public static readonly KeyTarget D0 = new (NamedKey.D0);
		}

		public static class Whitespace
		{
			// ── Whitespace / editing ───────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.Enter"/>
			public static readonly KeyTarget Enter = new (NamedKey.Enter);

			/// <inheritdoc cref="NamedKey.Escape"/>
			public static readonly KeyTarget Escape = new (NamedKey.Escape);

			/// <inheritdoc cref="NamedKey.Backspace"/>
			public static readonly KeyTarget Backspace = new (NamedKey.Backspace);

			/// <inheritdoc cref="NamedKey.Tab"/>
			public static readonly KeyTarget Tab = new (NamedKey.Tab);

			/// <inheritdoc cref="NamedKey.Space"/>
			public static readonly KeyTarget Space = new (NamedKey.Space);
		}

		public static class Punctuation
		{
			// ── Punctuation ────────────────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.Minus"/>
			public static readonly KeyTarget Minus = new (NamedKey.Minus);

			/// <inheritdoc cref="NamedKey.Equals"/>
			// ReSharper disable MemberHidesStaticFromOuterClass
			public new static readonly KeyTarget Equals = new (NamedKey.Equals);
			// ReSharper restore MemberHidesStaticFromOuterClass

			/// <inheritdoc cref="NamedKey.LeftBracket"/>
			public static readonly KeyTarget LeftBracket = new (NamedKey.LeftBracket);

			/// <inheritdoc cref="NamedKey.RightBracket"/>
			public static readonly KeyTarget RightBracket = new (NamedKey.RightBracket);

			/// <inheritdoc cref="NamedKey.Backslash"/>
			public static readonly KeyTarget Backslash = new (NamedKey.Backslash);

			/// <inheritdoc cref="NamedKey.Semicolon"/>
			public static readonly KeyTarget Semicolon = new (NamedKey.Semicolon);

			/// <inheritdoc cref="NamedKey.Quote"/>
			public static readonly KeyTarget Quote = new (NamedKey.Quote);

			/// <inheritdoc cref="NamedKey.Backtick"/>
			public static readonly KeyTarget Backtick = new (NamedKey.Backtick);

			/// <inheritdoc cref="NamedKey.Comma"/>
			public static readonly KeyTarget Comma = new (NamedKey.Comma);

			/// <inheritdoc cref="NamedKey.Period"/>
			public static readonly KeyTarget Period = new (NamedKey.Period);

			/// <inheritdoc cref="NamedKey.Slash"/>
			public static readonly KeyTarget Slash = new (NamedKey.Slash);
		}

		public static class FunctionKeys
		{
			// ── Function keys ──────────────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.F1"/>
			public static readonly KeyTarget F1 = new (NamedKey.F1);

			/// <inheritdoc cref="NamedKey.F2"/>
			public static readonly KeyTarget F2 = new (NamedKey.F2);

			/// <inheritdoc cref="NamedKey.F3"/>
			public static readonly KeyTarget F3 = new (NamedKey.F3);

			/// <inheritdoc cref="NamedKey.F4"/>
			public static readonly KeyTarget F4 = new (NamedKey.F4);

			/// <inheritdoc cref="NamedKey.F5"/>
			public static readonly KeyTarget F5 = new (NamedKey.F5);

			/// <inheritdoc cref="NamedKey.F6"/>
			public static readonly KeyTarget F6 = new (NamedKey.F6);

			/// <inheritdoc cref="NamedKey.F7"/>
			public static readonly KeyTarget F7 = new (NamedKey.F7);

			/// <inheritdoc cref="NamedKey.F8"/>
			public static readonly KeyTarget F8 = new (NamedKey.F8);

			/// <inheritdoc cref="NamedKey.F9"/>
			public static readonly KeyTarget F9 = new (NamedKey.F9);

			/// <inheritdoc cref="NamedKey.F10"/>
			public static readonly KeyTarget F10 = new (NamedKey.F10);

			/// <inheritdoc cref="NamedKey.F11"/>
			public static readonly KeyTarget F11 = new (NamedKey.F11);

			/// <inheritdoc cref="NamedKey.F12"/>
			public static readonly KeyTarget F12 = new (NamedKey.F12);

			/// <inheritdoc cref="NamedKey.F13"/>
			public static readonly KeyTarget F13 = new (NamedKey.F13);

			/// <inheritdoc cref="NamedKey.F14"/>
			public static readonly KeyTarget F14 = new (NamedKey.F14);

			/// <inheritdoc cref="NamedKey.F15"/>
			public static readonly KeyTarget F15 = new (NamedKey.F15);

			/// <inheritdoc cref="NamedKey.F16"/>
			public static readonly KeyTarget F16 = new (NamedKey.F16);

			/// <inheritdoc cref="NamedKey.F17"/>
			public static readonly KeyTarget F17 = new (NamedKey.F17);

			/// <inheritdoc cref="NamedKey.F18"/>
			public static readonly KeyTarget F18 = new (NamedKey.F18);

			/// <inheritdoc cref="NamedKey.F19"/>
			public static readonly KeyTarget F19 = new (NamedKey.F19);

			/// <inheritdoc cref="NamedKey.F20"/>
			public static readonly KeyTarget F20 = new (NamedKey.F20);

			/// <inheritdoc cref="NamedKey.F21"/>
			public static readonly KeyTarget F21 = new (NamedKey.F21);

			/// <inheritdoc cref="NamedKey.F22"/>
			public static readonly KeyTarget F22 = new (NamedKey.F22);

			/// <inheritdoc cref="NamedKey.F23"/>
			public static readonly KeyTarget F23 = new (NamedKey.F23);

			/// <inheritdoc cref="NamedKey.F24"/>
			public static readonly KeyTarget F24 = new (NamedKey.F24);
		}

		public static class NavigationOrSystem
		{
			// ── Navigation / system ────────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.CapsLock"/>
			public static readonly KeyTarget CapsLock = new (NamedKey.CapsLock);

			/// <inheritdoc cref="NamedKey.PrintScreen"/>
			public static readonly KeyTarget PrintScreen = new (NamedKey.PrintScreen);

			/// <inheritdoc cref="NamedKey.ScrollLock"/>
			public static readonly KeyTarget ScrollLock = new (NamedKey.ScrollLock);

			/// <inheritdoc cref="NamedKey.Pause"/>
			public static readonly KeyTarget Pause = new (NamedKey.Pause);

			/// <inheritdoc cref="NamedKey.Insert"/>
			public static readonly KeyTarget Insert = new (NamedKey.Insert);

			/// <inheritdoc cref="NamedKey.Home"/>
			public static readonly KeyTarget Home = new (NamedKey.Home);

			/// <inheritdoc cref="NamedKey.PageUp"/>
			public static readonly KeyTarget PageUp = new (NamedKey.PageUp);

			/// <inheritdoc cref="NamedKey.Delete"/>
			public static readonly KeyTarget Delete = new (NamedKey.Delete);

			/// <inheritdoc cref="NamedKey.End"/>
			public static readonly KeyTarget End = new (NamedKey.End);

			/// <inheritdoc cref="NamedKey.PageDown"/>
			public static readonly KeyTarget PageDown = new (NamedKey.PageDown);

			/// <inheritdoc cref="NamedKey.ArrowRight"/>
			public static readonly KeyTarget ArrowRight = new (NamedKey.ArrowRight);

			/// <inheritdoc cref="NamedKey.ArrowLeft"/>
			public static readonly KeyTarget ArrowLeft = new (NamedKey.ArrowLeft);

			/// <inheritdoc cref="NamedKey.ArrowDown"/>
			public static readonly KeyTarget ArrowDown = new (NamedKey.ArrowDown);

			/// <inheritdoc cref="NamedKey.ArrowUp"/>
			public static readonly KeyTarget ArrowUp = new (NamedKey.ArrowUp);

			/// <inheritdoc cref="NamedKey.Application"/>
			public static readonly KeyTarget Application = new (NamedKey.Application);
		}

		public static class Keypad
		{
			// ── Keypad ─────────────────────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.NumLock"/>
			public static readonly KeyTarget NumLock = new (NamedKey.NumLock);

			/// <inheritdoc cref="NamedKey.KeypadDivide"/>
			public static readonly KeyTarget KeypadDivide = new (NamedKey.KeypadDivide);

			/// <inheritdoc cref="NamedKey.KeypadMultiply"/>
			public static readonly KeyTarget KeypadMultiply = new (NamedKey.KeypadMultiply);

			/// <inheritdoc cref="NamedKey.KeypadMinus"/>
			public static readonly KeyTarget KeypadMinus = new (NamedKey.KeypadMinus);

			/// <inheritdoc cref="NamedKey.KeypadPlus"/>
			public static readonly KeyTarget KeypadPlus = new (NamedKey.KeypadPlus);

			/// <inheritdoc cref="NamedKey.KeypadEnter"/>
			public static readonly KeyTarget KeypadEnter = new (NamedKey.KeypadEnter);

			/// <inheritdoc cref="NamedKey.Keypad1"/>
			public static readonly KeyTarget Keypad1 = new (NamedKey.Keypad1);

			/// <inheritdoc cref="NamedKey.Keypad2"/>
			public static readonly KeyTarget Keypad2 = new (NamedKey.Keypad2);

			/// <inheritdoc cref="NamedKey.Keypad3"/>
			public static readonly KeyTarget Keypad3 = new (NamedKey.Keypad3);

			/// <inheritdoc cref="NamedKey.Keypad4"/>
			public static readonly KeyTarget Keypad4 = new (NamedKey.Keypad4);

			/// <inheritdoc cref="NamedKey.Keypad5"/>
			public static readonly KeyTarget Keypad5 = new (NamedKey.Keypad5);

			/// <inheritdoc cref="NamedKey.Keypad6"/>
			public static readonly KeyTarget Keypad6 = new (NamedKey.Keypad6);

			/// <inheritdoc cref="NamedKey.Keypad7"/>
			public static readonly KeyTarget Keypad7 = new (NamedKey.Keypad7);

			/// <inheritdoc cref="NamedKey.Keypad8"/>
			public static readonly KeyTarget Keypad8 = new (NamedKey.Keypad8);

			/// <inheritdoc cref="NamedKey.Keypad9"/>
			public static readonly KeyTarget Keypad9 = new (NamedKey.Keypad9);

			/// <inheritdoc cref="NamedKey.Keypad0"/>
			public static readonly KeyTarget Keypad0 = new (NamedKey.Keypad0);

			/// <inheritdoc cref="NamedKey.KeypadDecimal"/>
			public static readonly KeyTarget KeypadDecimal = new (NamedKey.KeypadDecimal);

			/// <inheritdoc cref="NamedKey.KeypadEqual"/>
			public static readonly KeyTarget KeypadEqual = new (NamedKey.KeypadEqual);
		}

		public static class Modifiers
		{
			// ── Modifiers ──────────────────────────────────────────────────────────
			/// <inheritdoc cref="NamedKey.LeftControl"/>
			public static readonly KeyTarget LeftControl = new (NamedKey.LeftControl);

			/// <inheritdoc cref="NamedKey.LeftShift"/>
			public static readonly KeyTarget LeftShift = new (NamedKey.LeftShift);

			/// <inheritdoc cref="NamedKey.LeftAlt"/>
			public static readonly KeyTarget LeftAlt = new (NamedKey.LeftAlt);

			/// <inheritdoc cref="NamedKey.LeftGui"/>
			public static readonly KeyTarget LeftGui = new (NamedKey.LeftGui);

			/// <inheritdoc cref="NamedKey.RightControl"/>
			public static readonly KeyTarget RightControl = new (NamedKey.RightControl);

			/// <inheritdoc cref="NamedKey.RightShift"/>
			public static readonly KeyTarget RightShift = new (NamedKey.RightShift);

			/// <inheritdoc cref="NamedKey.RightAlt"/>
			public static readonly KeyTarget RightAlt = new (NamedKey.RightAlt);

			/// <inheritdoc cref="NamedKey.RightGui"/>
			public static readonly KeyTarget RightGui = new (NamedKey.RightGui);
		}

		public static class MediaOrBrowser
		{
			// ── Consumer page: media / browser / launch ────────────────────────────
			/// <inheritdoc cref="NamedKey.MediaPlayPause"/>
			public static readonly KeyTarget MediaPlayPause = new (NamedKey.MediaPlayPause);

			/// <inheritdoc cref="NamedKey.MediaNextTrack"/>
			public static readonly KeyTarget MediaNextTrack = new (NamedKey.MediaNextTrack);

			/// <inheritdoc cref="NamedKey.MediaPreviousTrack"/>
			public static readonly KeyTarget MediaPreviousTrack = new (NamedKey.MediaPreviousTrack);

			/// <inheritdoc cref="NamedKey.MediaStop"/>
			public static readonly KeyTarget MediaStop = new (NamedKey.MediaStop);

			/// <inheritdoc cref="NamedKey.VolumeMute"/>
			public static readonly KeyTarget VolumeMute = new (NamedKey.VolumeMute);

			/// <inheritdoc cref="NamedKey.VolumeUp"/>
			public static readonly KeyTarget VolumeUp = new (NamedKey.VolumeUp);

			/// <inheritdoc cref="NamedKey.VolumeDown"/>
			public static readonly KeyTarget VolumeDown = new (NamedKey.VolumeDown);

			/// <inheritdoc cref="NamedKey.BrowserHome"/>
			public static readonly KeyTarget BrowserHome = new (NamedKey.BrowserHome);

			/// <inheritdoc cref="NamedKey.BrowserBack"/>
			public static readonly KeyTarget BrowserBack = new (NamedKey.BrowserBack);

			/// <inheritdoc cref="NamedKey.BrowserForward"/>
			public static readonly KeyTarget BrowserForward = new (NamedKey.BrowserForward);

			/// <inheritdoc cref="NamedKey.BrowserRefresh"/>
			public static readonly KeyTarget BrowserRefresh = new (NamedKey.BrowserRefresh);

			/// <inheritdoc cref="NamedKey.BrowserSearch"/>
			public static readonly KeyTarget BrowserSearch = new (NamedKey.BrowserSearch);

			/// <inheritdoc cref="NamedKey.LaunchCalculator"/>
			public static readonly KeyTarget LaunchCalculator = new (NamedKey.LaunchCalculator);

			/// <inheritdoc cref="NamedKey.LaunchMail"/>
			public static readonly KeyTarget LaunchMail = new (NamedKey.LaunchMail);
		}
	}
}