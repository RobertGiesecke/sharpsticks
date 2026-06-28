namespace SharpSticks.InputAbstractions;

public interface ICombinedRoute : IRoute
{
	IEnumerable<IRoute> GetRoutes();
}