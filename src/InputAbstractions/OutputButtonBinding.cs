namespace SharpSticks.InputAbstractions;

public sealed record OutputButtonBinding(uint OutputDeviceId, int ButtonNumber) : ButtonTarget<OutputButtonBinding>
{
    public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
        context.CreateOutputButtonSink(this);

    protected override OutputButtonBinding Merge(MergeObjectContext context) => this;
}