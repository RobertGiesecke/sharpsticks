using System.Text;

namespace SharpSticks.Overlay.WireProtocol;

/// <summary>One descriptor entry, viewed in place over the frame bytes.</summary>
public readonly ref struct DeviceInfo
{
	public required bool IsOutput { get; init; }
	public required ReadOnlySpan<byte> NameBytes { get; init; }
	public required ReadOnlySpan<byte> AxesBytes { get; init; }
	public required byte ButtonCount { get; init; }

	public int AxisCount => AxesBytes.Length;

	/// <summary>The wire's one-byte axis identity — the typed accessor lives
	/// with the axis enum (Overlay.WebSockets' <c>GetAxis</c>).</summary>
	public byte GetAxisCode(int index) => AxesBytes[index];

	public string GetName() => Encoding.UTF8.GetString(NameBytes);

	/// <summary>Char count <see cref="GetNameSpan"/> needs; <c>NameBytes.Length</c>
	/// is always a safe upper bound.</summary>
	public int GetNameSpanSize() => Encoding.UTF8.GetCharCount(NameBytes);

	public ReadOnlySpan<char> GetNameSpan(Span<char> result)
	{
		var usedCount = Encoding.UTF8.GetChars(NameBytes, result);
		return result[..usedCount];
	}
}