namespace ScaledAxisCSharp.DirectInput;

public sealed record AxisBinding(
	int DeviceId,
	PhysicalAxis Axis,
	AxisMode Mode,
	bool Invert,
	double Deadzone);