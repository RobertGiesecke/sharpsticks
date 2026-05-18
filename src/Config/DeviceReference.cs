namespace SharpSticks.Config;

/// <summary>
/// A saved reference to a physical device, used to remap the platform-assigned
/// <see cref="DeviceId"/> on load. Captures the id the config was authored
/// against plus enough identity to find the device at load time (its product
/// <see cref="Name"/> and, when available, the platform's per-instance
/// <see cref="InstanceGuid"/>).
/// </summary>
public sealed class DeviceReference
{
	public required int DeviceId { get; set; }
	public required string Name { get; set; }

	/// <summary>
	/// The platform-supplied instance identity captured at save time
	/// (DirectInput's <c>guidInstance</c> on Windows). Optional because
	/// hand-authored configs may omit it.
	/// </summary>
	public Guid? InstanceGuid { get; set; }
}
