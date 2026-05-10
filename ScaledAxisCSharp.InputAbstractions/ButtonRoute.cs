namespace ScaledAxisCSharp.InputAbstractions;

public sealed record ButtonRoute(ButtonBinding Binding, uint OutputDeviceId, int TargetButton);