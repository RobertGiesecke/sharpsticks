namespace SharpSticks.InputAbstractions;

public sealed record OutputButtonBinding(uint OutputDeviceId, int ButtonNumber) : ButtonTarget
{
    public override IRoute CreateRoute(ButtonBinding source) => new ButtonRoute(source, this);

    public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
        context.CreateOutputButtonSink(this);
}