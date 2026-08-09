using System.Text;

namespace SharpSticks.Tests;

/// <summary>
/// The PIDVID Guid layout ties DirectInput and evdev identities together —
/// encode/decode must round-trip any (vendor, product) pair, the suffix must
/// spell out "PIDVID", and foreign Guids must be told apart.
/// </summary>
public sealed class ProductGuidEncoderTests
{
	[Theory]
	[InlineData((ushort)0x044F, (ushort)0x0402)] // Thrustmaster-style ids
	[InlineData((ushort)0x0000, (ushort)0x0000)]
	[InlineData((ushort)0xFFFF, (ushort)0xFFFF)]
	[InlineData((ushort)0x1234, (ushort)0xABCD)]
	public void Encode_RoundTrips(ushort vendor, ushort product)
	{
		var guid = ProductGuidEncoder.Encode(vendor, product);

		Assert.True(ProductGuidEncoder.TryDecode(guid, out var decodedVendor, out var decodedProduct));
		Assert.Equal(vendor, decodedVendor);
		Assert.Equal(product, decodedProduct);
	}

	[Fact]
	public void Encode_UsesThePidVidSuffix()
	{
		var bytes = ProductGuidEncoder.Encode(0x044F, 0x0402).ToByteArray();

		Assert.Equal("PIDVID", Encoding.ASCII.GetString(bytes, 10, 6));
		Assert.Equal(0, bytes[8]);
		Assert.Equal(0, bytes[9]);
	}

	[Fact]
	public void TryDecode_RejectsForeignGuids()
	{
		Assert.False(ProductGuidEncoder.TryDecode(
			Guid.Parse("12345678-1234-1234-1234-123456789abc"), out _, out _));
		Assert.False(ProductGuidEncoder.TryDecode(Guid.Empty, out _, out _));
	}

	[Fact]
	public void DistinctHardware_YieldsDistinctGuids()
	{
		Assert.NotEqual(
			ProductGuidEncoder.Encode(0x044F, 0x0402),
			ProductGuidEncoder.Encode(0x0402, 0x044F)); // swapped vendor/product
	}
}
