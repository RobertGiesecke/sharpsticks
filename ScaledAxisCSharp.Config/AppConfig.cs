using System.Runtime.Serialization;

namespace ScaledAxisCSharp.Config;

public sealed class AppConfig
{
	public int VJoyDeviceId { get; set; } = 1;
	public List<ButtonMapping> ButtonMappings { get; set; } = [];
	public List<AxisMapping> AxisMappings { get; set; } = [];
}