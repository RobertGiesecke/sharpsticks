namespace ScaledAxisCSharp.DirectInput;

public sealed record AxisBinding(
	int DeviceId,
	PhysicalAxis Axis,
	AxisMode Mode,
	bool Invert = false,
	double Deadzone = 0.0);