namespace ScaledAxisCSharp.Config;

public sealed class AppConfig
{
	public string? Name { get; set; }
	public uint VJoyDeviceId { get; set; } = 1;
	public List<ButtonMapping> ButtonMappings { get; set; } = [];
	public List<AxisMapping> AxisMappings { get; set; } = [];
}