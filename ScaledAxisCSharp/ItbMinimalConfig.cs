namespace ScaledAxisCSharp;

public sealed class ItbMinimalConfig
{
	public int VJoyDeviceId { get; set; } = 1;
	public int PollIntervalMs { get; set; } = 5;

	public string LeftDeviceName { get; set; } = "LEFT VPC Stick WarBRD";
	public string RightDeviceName { get; set; } = "RIGHT VPC Stick WarBRD";

	public string ModifierAxis { get; set; } = "u";
	public double ModifierMin { get; set; } = -1.0;
	public double ModifierMax { get; set; } = 1.0;

	public double NormalSlope { get; set; } = 1.0;
	public double ModifierPrecisionSlope { get; set; } = 0.184;
	public double HoldPrecisionSlope { get; set; } = 0.508;

	public bool EnableAxis5XOverride { get; set; }
	public string Axis5OverrideAxis { get; set; } = "r";
	public double Axis5OverrideDeadzone { get; set; } = 0.05;

	public int PulseMs { get; set; } = 50;
}
