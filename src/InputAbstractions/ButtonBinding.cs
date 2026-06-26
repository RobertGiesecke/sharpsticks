namespace SharpSticks.InputAbstractions;

public sealed record ButtonBinding(int DeviceId, int ButtonNumber) : InputBinding(DeviceId),
	IMergeableObject<ButtonBinding>
{
	public ButtonBinding Merge(MergeObjectContext context) => this;
}