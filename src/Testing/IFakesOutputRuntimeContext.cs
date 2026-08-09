namespace SharpSticks.Testing;

public interface IFakesOutputRuntimeContext : IOutputRuntimeContext<
	FakeJoystickDevice, FakeOutputDevice>
{
	new FakeTimeSource TimeSource { get; }
	ITimeSource IRuntimeContext.TimeSource => TimeSource;
}