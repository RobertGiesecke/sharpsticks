namespace SharpSticks.InputAbstractions;

public sealed record OutputButtonBinding(uint OutputDeviceId, int ButtonNumber) : ButtonTarget
{
    public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
        context.CreateOutputButtonSink(this);
}