namespace ScaledAxisCSharp.Config;

public sealed record AxisRoute(AxisBinding Source, VJoyAxis TargetAxis, double Scale, double Offset);