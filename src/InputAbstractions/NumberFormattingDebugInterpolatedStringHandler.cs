using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpSticks.InputAbstractions;

[InterpolatedStringHandler]
public ref struct NumberFormattingDebugInterpolatedStringHandler
{
	private readonly bool _IsEmpty;
	private readonly string _NumberFormat;
	private readonly int _MaxFormattedLength;
	private DefaultInterpolatedStringHandler _Handler;

	public static NumberFormattingDebugInterpolatedStringHandler Empty() => new(true, 0, 0);

	private NumberFormattingDebugInterpolatedStringHandler(
		bool isEmpty,
		int literalLength,
		int formattedCount,
		[StringSyntax(StringSyntaxAttribute.NumericFormat)]
		string numberFormat = "0.0000",
		int maxFormattedLength = 15)
	{
		_IsEmpty = isEmpty;
		_NumberFormat = numberFormat;
		_MaxFormattedLength = maxFormattedLength;
		_Handler = new(literalLength, formattedCount, CultureInfo.InvariantCulture);
	}

	public NumberFormattingDebugInterpolatedStringHandler(
		int literalLength,
		int formattedCount,
		[StringSyntax(StringSyntaxAttribute.NumericFormat)]
		string numberFormat = "0.0000",
		int maxFormattedLength = 15) : this(false, literalLength, formattedCount, numberFormat, maxFormattedLength)
	{
	}

	void ThrowIfEmpty()
	{
		if (!_IsEmpty)
		{
			return;
		}

		throw new InvalidOperationException("Cannot format an empty interpolated string.");
	}

	public void AppendFormatted(double value)
	{
		ThrowIfEmpty();
		Span<char> destination = stackalloc char[_MaxFormattedLength];
		if (!value.TryFormat(destination, out var chars, _NumberFormat, CultureInfo.InvariantCulture))
		{
			AppendFormatted("x.xxxx");
			return;
		}

		destination = destination[..chars];
		_Handler.AppendFormatted(destination);
	}

	public void AppendFormatted(DateTime value,
		[StringSyntax(StringSyntaxAttribute.DateTimeFormat)] ReadOnlySpan<char> format)
	{
		ThrowIfEmpty();
		Span<char> destination = stackalloc char[100];
		if (!value.TryFormat(destination, out var chars, format, CultureInfo.InvariantCulture))
		{
			AppendFormatted("xxxx-xx-xx");
			return;
		}

		destination = destination[..chars];
		_Handler.AppendFormatted(destination);
	}

	public void AppendFormatted(Guid value)
	{
		AppendFormatted(value, "d");
	}

	public void AppendFormatted(Guid value, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format)
	{
		ThrowIfEmpty();
		Span<char> destination = stackalloc char[70];
		if (!value.TryFormat(destination, out var chars, format))
		{
			AppendFormatted("xxxx-xx-xx");
			return;
		}

		destination = destination[..chars];
		_Handler.AppendFormatted(destination);
	}

	static int CountDigits(uint value)
	{
		// dotnet/runtime's table: upper 32 bits of each entry hold the digit count
		// for that Log2 bucket; the low bits bump the count by 1 once the value
		// crosses the power of ten inside the bucket.
		ReadOnlySpan<long> table =
		[
			4294967296, 8589934582, 8589934582, 8589934582, 12884901788, 12884901788, 12884901788, 17179868184,
			17179868184, 17179868184, 21474826480, 21474826480, 21474826480, 21474826480, 25769703776, 25769703776,
			25769703776, 30063771072, 30063771072, 30063771072, 34349738368, 34349738368, 34349738368, 34349738368,
			38554705664, 38554705664, 38554705664, 41949672960, 41949672960, 41949672960, 42949672960, 42949672960,
		];

		var tableValue = table[BitOperations.Log2(value)];
		return (int)((value + tableValue) >> 32);
	}

	static int CountDigits(int value) =>
		// the sign isn't a digit; the long round-trip makes int.MinValue safe
		CountDigits(value < 0 ? (uint)(-(long)value) : (uint)value);

	public void AppendFormatted(int value)
	{
		ThrowIfEmpty();
		// CountDigits excludes the sign — reserve one extra char for it.
		var maxChars = CountDigits(value) + (value < 0 ? 1 : 0);
		Span<char> destination = stackalloc char[maxChars];
		if (!value.TryFormat(destination, out var chars))
		{
			AppendFormatted("xxxx");
			return;
		}

		destination = destination[..chars];
		_Handler.AppendFormatted(destination);
	}

	public void AppendFormatted(uint value)
	{
		ThrowIfEmpty();
		var maxDigis = CountDigits(value);
		Span<char> destination = stackalloc char[maxDigis];
		if (!value.TryFormat(destination, out var chars))
		{
			AppendFormatted("xxxx");
			return;
		}

		destination = destination[..chars];
		_Handler.AppendFormatted(destination);
	}

#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
	public void AppendFormatted(AxisDecoderKind decoderKind) => AppendFormatted(decoderKind switch
	{
		AxisDecoderKind.Unknown => nameof(AxisDecoderKind.Unknown),
		AxisDecoderKind.NativeSigned => nameof(AxisDecoderKind.NativeSigned),
		AxisDecoderKind.UnsignedCentered => nameof(AxisDecoderKind.UnsignedCentered),
		AxisDecoderKind.Unsigned => nameof(AxisDecoderKind.Unsigned),
	});

	public void AppendFormatted(Axis axis) => AppendFormatted(axis switch
	{
		Axis.X => nameof(Axis.X),
		Axis.Y => nameof(Axis.Y),
		Axis.Z => nameof(Axis.Z),
		Axis.Rx => nameof(Axis.Rx),
		Axis.Ry => nameof(Axis.Ry),
		Axis.Rz => nameof(Axis.Rz),
		Axis.Slider1 => nameof(Axis.Slider1),
		Axis.Slider2 => nameof(Axis.Slider2),
	});


#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.

	public void AppendFormatted(scoped ReadOnlySpan<char> value)
	{
		ThrowIfEmpty();
		_Handler.AppendFormatted(value);
	}

	public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
	{
		ThrowIfEmpty();
		_Handler.AppendFormatted(value, alignment, format);
	}

	public void AppendFormatted(string? value)
	{
		ThrowIfEmpty();
		_Handler.AppendFormatted(value);
	}

	public void AppendFormatted(NumberFormattingDebugInterpolatedStringHandler value)
	{
		ThrowIfEmpty();
		_Handler.AppendFormatted(value.Text);

		// The inner handler's buffer is rented from the ArrayPool — hand it
		// back now that its text has been copied, or every nested
		// GetDebugView() abandons an array per log line.
		if (!value._IsEmpty)
		{
			value.Clear();
		}
	}

	public void AppendFormatted(string? value, int alignment = 0, string? format = null)
	{
		ThrowIfEmpty();
		_Handler.AppendFormatted(value, alignment, format);
	}

	public void AppendLiteral(string value)
	{
		ThrowIfEmpty();
		_Handler.AppendLiteral(value);
	}

	public void Clear()
	{
		ThrowIfEmpty();
		_Handler.Clear();
	}

	public string ToStringAndClear()
	{
		return _IsEmpty
			? ""
			: _Handler.ToStringAndClear();
	}

	public ReadOnlySpan<char> Text => _IsEmpty
		? ReadOnlySpan<char>.Empty
		: _Handler.Text;
}