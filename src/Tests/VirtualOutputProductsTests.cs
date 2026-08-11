namespace SharpSticks.Tests;

public sealed class VirtualOutputProductsTests
{
	[Fact]
	public void VJoyIdentity_IsRecognized()
	{
		Assert.True(VirtualOutputProducts.IsVirtualOutput(VirtualOutputProducts.VJoyProductGuid));
		Assert.True(VirtualOutputProducts.IsVirtualOutput(
			ProductGuidEncoder.Encode(VirtualOutputProducts.VJoyVendorId, VirtualOutputProducts.VJoyProductId)));
	}

	[Theory]
	[InlineData(1u)]
	[InlineData(7u)]
	[InlineData(255u)]
	public void UinputIdentity_IsRecognized_AcrossTheDeviceIdRange(uint deviceId)
	{
		var guid = ProductGuidEncoder.Encode(
			VirtualOutputProducts.UinputVendorId,
			(ushort)(VirtualOutputProducts.UinputProductBase | deviceId));

		Assert.True(VirtualOutputProducts.IsVirtualOutput(guid));
	}

	[Theory]
	// Physical hardware (VirPil's VID as an arbitrary real-world example).
	[InlineData((ushort)0x3344, (ushort)0x0194)]
	// vJoy's vendor with a different product.
	[InlineData((ushort)0x1234, (ushort)0x0001)]
	// The uinput vendor outside the 0xC0xx product window.
	[InlineData((ushort)0xFEED, (ushort)0xB000)]
	public void ForeignHidIdentities_AreNotVirtualOutputs(ushort vendor, ushort product)
	{
		Assert.False(VirtualOutputProducts.IsVirtualOutput(ProductGuidEncoder.Encode(vendor, product)));
	}

	[Fact]
	public void NonPidVidGuids_AreNotVirtualOutputs()
	{
		Assert.False(VirtualOutputProducts.IsVirtualOutput(Guid.Empty));
		Assert.False(VirtualOutputProducts.IsVirtualOutput(Guid.CreateVersion7()));
	}
}
