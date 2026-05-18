namespace SharpSticks.Config;

public sealed class AppConfig
{
	public string? Name { get; set; }
	public uint VJoyDeviceId { get; set; } = 1;

	/// <summary>
	/// Optional list of devices this config was authored against. Used at
	/// load time to map config-authored <c>DeviceId</c>s onto the
	/// platform-assigned ids of the currently connected devices (which
	/// can change between sessions). Empty = treat ids in bindings as
	/// literal current ids (the legacy behavior).
	/// </summary>
	public List<DeviceReference> Devices { get; set; } = [];

	public List<ButtonMapping> ButtonMappings { get; set; } = [];
	public List<AxisMapping> AxisMappings { get; set; } = [];
}