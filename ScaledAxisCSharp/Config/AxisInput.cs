namespace ScaledAxisCSharp.Config;

public sealed class AxisInput
{
	public int DeviceId { get; set; }
	public string Axis { get; set; } = "x";
	public string Mode { get; set; } = "signed";
	public bool Invert { get; set; }
	public double Deadzone { get; set; }
}