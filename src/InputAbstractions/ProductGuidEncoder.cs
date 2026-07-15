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

	/// Inverse of <see cref="Encode"/>: recovers (vendor, product) from a PIDVID-shaped Guid.
	/// Returns false for any Guid that isn't in this layout (wrong PIDVID suffix), so callers can
	/// tell an encoded HID identity apart from an arbitrary product Guid.
	public static bool TryDecode(Guid guid, out ushort vendor, out ushort product)
	{
		Span<byte> bytes = stackalloc byte[16];
		if (!guid.TryWriteBytes(bytes) || !bytes[8..].SequenceEqual(PidVidSuffix))
		{
			vendor = 0;
			product = 0;
			return false;
		}

		var pidVid = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
		vendor = (ushort)(pidVid & 0xffff);
		product = (ushort)(pidVid >> 16);
		return true;
	}
}
