namespace SharpSticks.Tests;

/// <summary>
/// Locks the encoding and conversions of the keyboard/mouse value types: HID
/// usage packing, implicit enum→struct conversions, keyboard/consumer page
/// non-collision, and round-tripping of unnamed usages reached through the
/// factory methods.
/// </summary>
public sealed class KeyAndMouseButtonTests
{
	[Fact]
	public void NamedKey_PacksKeyboardPageAndUsage()
	{
		Key a = NamedKey.A;
		Assert.Equal(Key.KeyboardUsagePage, a.UsagePage);
		Assert.Equal(0x04, a.Usage);
		Assert.Equal(NamedKey.A, (NamedKey)a.Code);
	}

	[Fact]
	public void NamedKey_F19_HasHidUsage0x6E()
	{
		Key f19 = NamedKey.F19;
		Assert.Equal(Key.KeyboardUsagePage, f19.UsagePage);
		Assert.Equal(0x6E, f19.Usage);
	}

	[Fact]
	public void ConsumerKey_PacksConsumerPage_AndDoesNotCollideWithKeyboardPage()
	{
		Key playPause = NamedKey.MediaPlayPause;
		Assert.Equal(Key.ConsumerUsagePage, playPause.UsagePage);
		Assert.Equal(0x00CD, playPause.Usage);

		// Keyboard usage 0xCD and consumer usage 0xCD are distinct keys.
		Assert.NotEqual(Key.FromKeyboard(0x00CD), Key.FromConsumer(0x00CD));
	}

	[Fact]
	public void UnnamedKeyboardUsage_RoundTrips_AndStringifiesByCode()
	{
		// 0x87 (International1 — layout-specific) is intentionally not in NamedKey.
		var key = Key.FromKeyboard(0x87);
		Assert.Equal(Key.KeyboardUsagePage, key.UsagePage);
		Assert.Equal(0x87, key.Usage);
		Assert.False(Enum.IsDefinedFast((NamedKey)key.Code));
		Assert.Equal($"Key(0x{key.Code:X})", key.ToString());
	}

	[Fact]
	public void NamedKey_StringifiesByName()
	{
		Key enter = NamedKey.Enter;
		Assert.Equal("Enter", enter.ToString());
	}

	[Fact]
	public void NamedMouseButton_ConvertsToOneBasedIndex()
	{
		MouseButton middle = NamedMouseButton.Middle;
		Assert.Equal(3, middle.Index);
		Assert.Equal("Middle", middle.ToString());
	}

	[Fact]
	public void ExtendedMouseButton_HasNoName_ButRoundTrips()
	{
		var button6 = new MouseButton(6);
		Assert.Equal(6, button6.Index);
		Assert.False(Enum.IsDefinedFast((NamedMouseButton)button6.Index));
		Assert.Equal("MouseButton(6)", button6.ToString());
	}
}
