namespace ScaledAxisCSharp.Config;

public sealed class AppConfig
{
	public int VJoyDeviceId { get; set; } = 1;
	public int PollIntervalMs { get; set; } = 8;
	public List<ButtonMapping> ButtonMappings { get; set; } = [];
	public List<AxisMapping> AxisMappings { get; set; } = [];
	public List<ScaledAxisMapping> ScaledAxisMappings { get; set; } = [];
}