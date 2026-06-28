namespace SharpSticks.InputAbstractions;

public interface IBoundRoute : IConfigurableRoute, IMergeableObject
{
	InputBinding InputBinding { get; }
}