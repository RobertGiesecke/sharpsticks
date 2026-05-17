namespace ScaledAxisCSharp.InputAbstractions;

public readonly record struct AxisDebugSample(
	int RawValue,
	int RangeMin,
	int RangeMax,
	double NormalizedValue,
	AxisDecoderKind DecoderKind);