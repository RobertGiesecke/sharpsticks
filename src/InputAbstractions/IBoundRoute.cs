namespace SharpSticks.InputAbstractions;

public interface IBoundRoute : IRoute, IMergeableObject
{
	InputBinding InputBinding { get; }
}