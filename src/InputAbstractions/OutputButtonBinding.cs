namespace SharpSticks.InputAbstractions;

public readonly record struct OutputButtonBinding(uint OutputDeviceId, int ButtonNumber) : IButtonTarget
{
    public IRoute CreateRoute(ButtonBinding source) => new ButtonRoute(source, this);
}