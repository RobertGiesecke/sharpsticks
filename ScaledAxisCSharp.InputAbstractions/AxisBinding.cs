namespace ScaledAxisCSharp.InputAbstractions;

public sealed record AxisBinding(
	int DeviceId,
	PhysicalAxis Axis,
	AxisMode Mode = AxisMode.Signed,
	bool Invert = false,
	double Deadzone = 0.0);