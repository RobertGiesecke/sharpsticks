namespace ScaledAxisCSharp.InputAbstractions;

public sealed record ButtonRoute(ButtonBinding Binding, uint VJoyDeviceId, int TargetButton);