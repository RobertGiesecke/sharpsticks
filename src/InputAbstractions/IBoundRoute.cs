namespace SharpSticks.InputAbstractions;

public interface IBoundRoute : IRoute
{
	InputBinding InputBinding { get; }
	uint OutputDeviceId { get; }
}