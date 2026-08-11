namespace SharpSticks.InputAbstractions;

/// HID identities of the virtual output devices SharpSticks feeds. Their input-side
/// mirrors (vJoy's DirectInput entry on Windows, the uinput evdev node on Linux)
/// enumerate like any other input device; readers that must not interfere with the
/// feeder side — e.g. the standalone overlay server, which would otherwise block the
/// routing engine from acquiring vJoy — use this to recognize and skip them.
public static class VirtualOutputProducts
{
	/// vJoy's fixed HID identity; every vJoy device slot shares it.
	public const ushort VJoyVendorId = 0x1234;

	public const ushort VJoyProductId = 0xBEAD;

	/// SharpSticks' uinput devices are stamped with this vendor and a product of
	/// <see cref="UinputProductBase"/> | deviceId (low byte carries the id).
	public const ushort UinputVendorId = 0xFEED;

	public const ushort UinputProductBase = 0xC000;

	public static Guid VJoyProductGuid { get; } = ProductGuidEncoder.Encode(VJoyVendorId, VJoyProductId);

	public static bool IsVirtualOutput(Guid productGuid) =>
		ProductGuidEncoder.TryDecode(productGuid, out var vendor, out var product)
		&& ((vendor, product) == (VJoyVendorId, VJoyProductId)
		    || (vendor == UinputVendorId && (product & 0xFF00) == UinputProductBase));
}
