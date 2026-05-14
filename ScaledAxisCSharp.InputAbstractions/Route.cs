namespace ScaledAxisCSharp.InputAbstractions;

public interface IRoute
{
	InputBinding InputBinding { get; }
	uint OutputDeviceId { get; }
}

public abstract record Route : IRoute
{
	protected abstract InputBinding InputBinding { get; }
	protected abstract uint OutputDeviceId { get; }

	InputBinding IRoute.InputBinding => InputBinding;
	uint IRoute.OutputDeviceId => OutputDeviceId;
}