namespace ScaledAxisCSharp.InputAbstractions;

public sealed record ButtonBinding(int DeviceId, int ButtonNumber): InputBinding(DeviceId);