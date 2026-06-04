namespace SharpSticks.InputAbstractions;

public interface ICombinedRoute : IRoute
{
	IEnumerable<IBoundRoute> GetRoutes();
}