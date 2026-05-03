namespace ScaledAxisCSharp.Config;

internal sealed record AxisRoute(AxisBinding Source, VJoyAxis TargetAxis, double Scale, double Offset);
