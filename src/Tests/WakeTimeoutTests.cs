namespace SharpSticks.Tests;

/// <summary>
/// The run loop sleeps on <c>WaitAny</c> with the timeout
/// <c>ComputeWaitTimeoutMs</c> derives from the earliest macro / zone-pulse
/// deadline. A live deadline must never round down to a shorter wait that
/// re-enters the loop before it's due — waking a fraction of a millisecond
/// early re-runs ProcessFrame without firing anything and turns the wait into
/// a busy spin until the deadline finally passes.
/// </summary>
public sealed class WakeTimeoutTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public WakeTimeoutTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Output = _Fakes.AddOutputDevice().AddButtons(8).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void IdleMacroRoute_WaitsForever()
	{
		using var runtime = Build(WaitMacro(50.Milliseconds));

		runtime.ProcessWithDefaultFrameTime();
		Assert.Equal(System.Threading.Timeout.Infinite, runtime.ComputeWaitTimeoutMs());
	}

	[Fact]
	public void SleepingMacro_DrivesTheWakeTimeout_WithTheRemainingTime()
	{
		using var runtime = Build(WaitMacro(50.Milliseconds));

		StartWait(runtime);
		Assert.Equal(50, runtime.ComputeWaitTimeoutMs());
	}

	[Fact]
	public void SubMillisecondRemainder_RoundsUpToAWholeMillisecond()
	{
		using var runtime = Build(WaitMacro(50.Milliseconds));

		StartWait(runtime);

		// Step to 0.4 ms before the deadline — the macro must still hold...
		runtime.ProcessFrame(49.6.Milliseconds);
		Assert.True(_Output.GetButtonState(3));

		// ...and the wake timeout must round UP to 1 ms. Truncating to 0 makes
		// WaitAny return immediately and the run loop spin full frames until
		// the deadline passes.
		Assert.Equal(1, runtime.ComputeWaitTimeoutMs());
	}

	[Fact]
	public void PassedDeadline_WakesImmediately()
	{
		using var runtime = Build(WaitMacro(50.Milliseconds));

		StartWait(runtime);

		// Time passes without a frame (the loop was busy elsewhere).
		runtime.TimeSource.Advance(60.Milliseconds);
		Assert.Equal(0, runtime.ComputeWaitTimeoutMs());
	}

	[Fact]
	public void ZonePulseDeadline_DrivesTheWakeTimeout()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.3, 0.6, _Output.BindButton(2),
			new() { Mode = AxisZoneTriggerMode.Pulse, PulseDuration = 50.Milliseconds }));

		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(2));

		Assert.Equal(50, runtime.ComputeWaitTimeoutMs());
	}

	private ButtonMacroRoute WaitMacro(TimeSpan wait) => new()
	{
		Binding = _Stick.BindButton(1),
		OnPress =
		[
			Macros.Press(_Output.BindButton(3)),
			Macros.WaitFor(wait),
			Macros.Release(_Output.BindButton(3)),
		],
	};

	/// <summary>Warm up, press, and run the frame that anchors the wait.</summary>
	private void StartWait(IFakesOutputRuntimeContext runtime)
	{
		runtime.ProcessWithDefaultFrameTime();
		_Stick.PressButton(1);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(3));
	}

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});
}
