using System.Globalization;

namespace SharpSticks.Tests;

/// <summary>
/// Black-box behavioral coverage of
/// <see cref="NumberFormattingDebugInterpolatedStringHandler"/>, written from
/// the spec only. The type is a <c>ref struct</c>, so handlers are built as
/// locals in each test and exception assertions use manual try/catch.
/// </summary>
public sealed class NumberFormattingDebugInterpolatedStringHandlerTests
{
	// 1. Empty sentinel.
	[Fact]
	public void Empty_ToStringAndClear_IsEmptyString()
	{
		var h = NumberFormattingDebugInterpolatedStringHandler.Empty();
		Assert.Equal("", h.ToStringAndClear());
	}

	[Fact]
	public void Empty_Text_IsEmpty()
	{
		var h = NumberFormattingDebugInterpolatedStringHandler.Empty();
		Assert.True(h.Text.IsEmpty);
	}

	[Fact]
	public void Empty_AppendFormattedDouble_Throws()
	{
		var h = NumberFormattingDebugInterpolatedStringHandler.Empty();
		var threw = false;
		try { h.AppendFormatted(1.0); }
		catch (InvalidOperationException) { threw = true; }
		Assert.True(threw);
	}

	[Fact]
	public void Empty_AppendLiteral_Throws()
	{
		var h = NumberFormattingDebugInterpolatedStringHandler.Empty();
		var threw = false;
		try { h.AppendLiteral("x"); }
		catch (InvalidOperationException) { threw = true; }
		Assert.True(threw);
	}

	[Fact]
	public void Empty_Clear_Throws()
	{
		var h = NumberFormattingDebugInterpolatedStringHandler.Empty();
		var threw = false;
		try { h.Clear(); }
		catch (InvalidOperationException) { threw = true; }
		Assert.True(threw);
	}

	// 2. Default double format.
	[Theory]
	[InlineData(1.5, "1.5000")]
	[InlineData(0.0, "0.0000")]
	[InlineData(-2.25, "-2.2500")]
	public void DefaultDoubleFormat(double input, string expected)
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(input);
		Assert.Equal(expected, h.ToStringAndClear());
	}

	// 3. Custom numeric format.
	[Fact]
	public void CustomNumberFormat_TwoDecimals()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1, numberFormat: "0.00");
		h.AppendFormatted(1.5);
		Assert.Equal("1.50", h.ToStringAndClear());
	}

	[Fact]
	public void CustomNumberFormat_OneDecimal_Rounds()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1, numberFormat: "0.0");
		h.AppendFormatted(3.14159);
		Assert.Equal("3.1", h.ToStringAndClear());
	}

	// 4. Invariant decimal separator.
	[Fact]
	public void InvariantDecimalSeparator_NoThousandsGrouping()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(1234.5);
		Assert.Equal("1234.5000", h.ToStringAndClear());
	}

	// 5. int formatting.
	[Theory]
	[InlineData(0, "0")]
	[InlineData(12345, "12345")]
	[InlineData(-7, "-7")]
	[InlineData(int.MinValue, "-2147483648")]
	[InlineData(int.MaxValue, "2147483647")]
	public void IntFormatting(int input, string expected)
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(input);
		Assert.Equal(expected, h.ToStringAndClear());
	}

	// 6. uint formatting.
	[Theory]
	[InlineData(0u, "0")]
	[InlineData(uint.MaxValue, "4294967295")]
	public void UIntFormatting(uint input, string expected)
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(input);
		Assert.Equal(expected, h.ToStringAndClear());
	}

	// 7. Literals + ordering.
	[Fact]
	public void LiteralThenFormatted_Concatenates()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendLiteral("v=");
		h.AppendFormatted(2);
		Assert.Equal("v=2", h.ToStringAndClear());
	}

	[Fact]
	public void ThreeAppends_ConcatenateInCallOrder()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 3);
		h.AppendLiteral("a");
		h.AppendFormatted(1);
		h.AppendLiteral("b");
		Assert.Equal("a1b", h.ToStringAndClear());
	}

	// 8. String/span overloads + alignment.
	[Fact]
	public void StringOverload_AppendsValue()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted("abc");
		Assert.Equal("abc", h.ToStringAndClear());
	}

	[Fact]
	public void NullString_AppendsNothing()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted((string?)null);
		Assert.Equal("", h.ToStringAndClear());
	}

	[Fact]
	public void StringAlignment_Positive_RightAligns()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted("ab", 5, null);
		Assert.Equal("   ab", h.ToStringAndClear());
	}

	[Fact]
	public void StringAlignment_Negative_LeftAligns()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted("ab", -5, null);
		Assert.Equal("ab   ", h.ToStringAndClear());
	}

	// 9. Axis enum.
	[Fact]
	public void Axis_Rx_FormatsAsMemberName()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(Axis.Rx);
		Assert.Equal("Rx", h.ToStringAndClear());
	}

	[Fact]
	public void Axis_Slider1_FormatsAsMemberName()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(Axis.Slider1);
		Assert.Equal("Slider1", h.ToStringAndClear());
	}

	[Fact]
	public void Axis_X_FormatsAsMemberName()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(Axis.X);
		Assert.Equal("X", h.ToStringAndClear());
	}

	// 10. Guid default + explicit format.
	[Fact]
	public void Guid_Default_FormatsAsD()
	{
		var g = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(g);
		Assert.Equal(g.ToString("d"), h.ToStringAndClear());
	}

	[Fact]
	public void Guid_ExplicitFormatN()
	{
		var g = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(g, "N");
		Assert.Equal(g.ToString("N"), h.ToStringAndClear());
	}

	// 11. DateTime.
	[Fact]
	public void DateTime_CustomFormat_Invariant()
	{
		var dt = new DateTime(2026, 6, 28, 13, 5, 9);
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		h.AppendFormatted(dt, "yyyy-MM-dd HH:mm:ss");
		Assert.Equal(dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), h.ToStringAndClear());
	}

	// 12. Clear resets.
	[Fact]
	public void Clear_ResetsAndAllowsFreshAppend()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 2);
		h.AppendFormatted("x");
		h.Clear();
		Assert.True(h.Text.IsEmpty);
		h.AppendFormatted("y");
		Assert.Equal("y", h.ToStringAndClear());
	}

	// 13. Nested handler.
	[Fact]
	public void NestedHandler_FlattensIntoOuter()
	{
		var inner = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		inner.AppendFormatted(2.5);

		var outer = new NumberFormattingDebugInterpolatedStringHandler(0, 1);
		outer.AppendLiteral("[");
		outer.AppendFormatted(inner);
		outer.AppendLiteral("]");

		Assert.Equal("[2.5000]", outer.ToStringAndClear());
	}

	// 14. (behavioral) Oversized format does not throw.
	[Fact]
	public void OversizedNumberFormat_DoesNotThrow()
	{
		var h = new NumberFormattingDebugInterpolatedStringHandler(0, 1, numberFormat: "0.000000000000000000000000");
		h.AppendFormatted(1.5);
		var result = h.ToStringAndClear();
		Assert.False(string.IsNullOrEmpty(result));
	}
}
