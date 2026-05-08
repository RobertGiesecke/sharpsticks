using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.DirectInput;

internal readonly record struct AxisFormatEntry(PhysicalAxis Axis, uint Offset, uint Type);