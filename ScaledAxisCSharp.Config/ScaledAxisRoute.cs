using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Config;

public sealed record ScaledAxisRoute(
	AxisBinding ValueSource,
	AxisBinding FactorSource,
	PhysicalAxis VJoyAxis,
	double FactorLow,
	double FactorHigh,
	double OutputScale,
	double OutputOffset);