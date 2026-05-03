using System.Text.Json.Serialization;

namespace ScaledAxisCSharp;

public sealed class AppConfig
{
	public int VJoyDeviceId { get; set; } = 1;
	public int PollIntervalMs { get; set; } = 8;
	public List<ButtonMapping> ButtonMappings { get; set; } = [];
	public List<AxisMapping> AxisMappings { get; set; } = [];
	public List<ScaledAxisMapping> ScaledAxisMappings { get; set; } = [];
}

public sealed class ButtonMapping
{
	public int SourceDeviceId { get; set; }
	public int SourceButton { get; set; }
	public int TargetButton { get; set; }
}

public sealed class AxisMapping
{
	public AxisInput Source { get; set; } = new();
	public string TargetAxis { get; set; } = "x";
	public double Scale { get; set; } = 1.0;
	public double Offset { get; set; }
}

public sealed class ScaledAxisMapping
{
	public AxisInput ValueSource { get; set; } = new();

	public AxisInput FactorSource { get; set; } = new()
	{
		Mode = "unsigned",
	};

	public string TargetAxis { get; set; } = "x";
	public double FactorLow { get; set; } = 0.5;
	public double FactorHigh { get; set; } = 1.0;
	public double OutputScale { get; set; } = 1.0;
	public double OutputOffset { get; set; }
}

public sealed class AxisInput
{
	public int DeviceId { get; set; }
	public string Axis { get; set; } = "x";
	public string Mode { get; set; } = "signed";
	public bool Invert { get; set; }
	public double Deadzone { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ItbMinimalConfig))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
