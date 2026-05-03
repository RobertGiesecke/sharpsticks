namespace ScaledAxisCSharp.Config;

internal sealed record ScaledAxisRoute(
	AxisBinding ValueSource,
	AxisBinding FactorSource,
	VJoyAxis TargetAxis,
	double FactorLow,
	double FactorHigh,
	double OutputScale,
	double OutputOffset);
