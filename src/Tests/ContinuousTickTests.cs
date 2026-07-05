namespace SharpSticks.Tests;

/// <summary>
/// While a continuous integrator (relative mouse/scroll or an absolute-relative axis) is
/// live, the run loop floors its wait to <c>UpdateInterval</c> so <c>ProcessFrame</c> keeps
/// ticking during input gaps; otherwise it waits indefinitely for a device event.
/// <see cref="IOutputRuntimeContext{TInputDevice,TOutputDevice}.ComputeWaitTimeoutMs"/> is
/// exposed so this is assertable without driving the real wait loop.
/// </summary>
public sealed class ContinuousTickTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public ContinuousTickTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void NoContinuousRoute_WaitsForDeviceEvent()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteTo(_Output.BindAxis(Axis.X)));
		Assert.Equal(System.Threading.Timeout.Infinite, runtime.ComputeWaitTimeoutMs());
	}

	[Fact]
	public void AbsoluteRelativeRoute_FloorsWaitToDefaultInterval()
	{
		using var runtime = Build(Zoom());
		Assert.Equal(4, runtime.ComputeWaitTimeoutMs()); // default ~250 Hz
	}

	[Fact]
	public void UpdateInterval_OverridesTheDefaultFloor()
	{
		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			UpdateInterval = TimeSpan.FromMilliseconds(10),
			Routes = [Zoom()],
		});

		Assert.Equal(10, runtime.ComputeWaitTimeoutMs());
	}

	// A bidirectional absolute-relative zoom (both directions on one output axis) — the
	// modifier integrates over time, so its runtime modifier is a continuous integrator.
	private AbsoluteRelativeAxisRoute Zoom() => _Stick.BindAxis(Axis.X).RouteAbsoluteRelative(new()
	{
		IncreaseAxis = _Output.BindAxis(Axis.X),
		DecreaseAxis = _Output.BindAxis(Axis.X),
		SourceInputMinimum = -1.0,
		SourceInputMaximum = 1.0,
		Gain = 6.0,
		IncreaseTimeToFull = TimeSpan.FromSeconds(1),
		DecreaseTimeToFull = TimeSpan.FromSeconds(1),
	});

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});
}
