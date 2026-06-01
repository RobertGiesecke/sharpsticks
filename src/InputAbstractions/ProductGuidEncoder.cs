using System.Buffers.Binary;

namespace SharpSticks.InputAbstractions;

/// Encodes HID (vendor, product) into the DirectInput-flavoured "PIDVID" Guid layout
/// <c>{(pid&lt;&lt;16)|vid, 0x0000, 0x0000, 00 00 'P' 'I' 'D' 'V' 'I' 'D'}</c>.
/// DirectInput natively reports this Guid as <c>guidProduct</c>; the Linux evdev backend
/// synthesises the same Guid from <c>input_id.vendor</c> / <c>input_id.product</c> so the
/// same hardware kind hashes to the same Guid on every platform.
public static class ProductGuidEncoder
{
	private static ReadOnlySpan<byte> PidVidSuffix => "\0\0PIDVID"u8;
	
	public static Guid Encode(ushort vendor, ushort product)
	{
		var pidVid = ((uint)product << 16) | vendor;
		Span<byte> bytes = stackalloc byte[16];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, pidVid);
		// bytes[4..8] stay zero (Guid data2 + data3)
		PidVidSuffix.CopyTo(bytes[8..]);
		return new(bytes);
	}
}
