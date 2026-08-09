namespace SharpSticks.Tests;

/// <summary>
/// Pins the pulse zone's wall-clock boundaries: the pulse releases exactly
/// when <c>now &gt;= deadline</c> (never a tick early), a zero-duration pulse
/// asserts for exactly one frame, and a live pulse is independent of the zone
/// — leaving or re-entering neither cuts it short nor re-triggers it; only
/// leaving after expiry re-arms.
/// </summary>
public sealed class AxisZonePulseTimingTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public AxisZonePulseTimingTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Output = _Fakes.AddOutputDevice().AddButtons(8).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Pulse_HoldsOneTickBeforeTheDeadline_AndReleasesExactlyOnIt()
	{
		using var runtime = Build(PulseRoute(50.Milliseconds));

		// Entering the zone starts the pulse at this frame's instant.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(2));

		// One tick short of the deadline: still pressed.
		runtime.ProcessFrame(50.Milliseconds - FromTicks(1));
		Assert.True(_Output.GetButtonState(2));

		// Exactly on the deadline: released.
		runtime.ProcessFrame(FromTicks(1));
		Assert.False(_Output.GetButtonState(2));
	}

	[Fact]
	public void Build_RejectsNonPositivePulseDuration()
	{
		// A zero-length pulse would assert and expire within the same instant —
		// the builder rejects it up front rather than leaving a do-nothing route.
		var ex = Assert.Throws<InvalidOperationException>(() => Build(PulseRoute(TimeSpan.Zero)));
		Assert.Contains("PulseDuration", ex.Message);
	}

	[Fact]
	public void Pulse_ExitAndReentryWhileLive_NeitherCutsShortNorRetriggers()
	{
		using var runtime = Build(PulseRoute(50.Milliseconds));

		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(2));

		// Leaving the zone does not cut the pulse short.
		_Stick.SetAxisValue(Axis.X, 0.1);
		runtime.ProcessFrame(10.Milliseconds);
		Assert.True(_Output.GetButtonState(2));

		// Re-entering while live does not extend or restart it.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame(10.Milliseconds);
		Assert.True(_Output.GetButtonState(2));

		// Expiry with the axis in range → cooldown, not a fresh pulse.
		runtime.ProcessFrame(40.Milliseconds);
		Assert.False(_Output.GetButtonState(2));
		runtime.ProcessWithDefaultFrameTime();
		Assert.False(_Output.GetButtonState(2));

		// Leaving clears the cooldown; the next entry pulses again.
		_Stick.SetAxisValue(Axis.X, 0.1);
		runtime.ProcessWithDefaultFrameTime();
		Assert.False(_Output.GetButtonState(2));
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(2));
	}

	private IConfigurableRoute PulseRoute(TimeSpan duration) =>
		_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.3, 0.6, _Output.BindButton(2),
			new() { Mode = AxisZoneTriggerMode.Pulse, PulseDuration = duration });

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});
}
