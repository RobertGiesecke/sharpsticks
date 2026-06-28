namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// Relative axis→mouse routing: the normalized axis drives pointer velocity
/// (pixels/sec at full deflection × elapsed seconds), with a sub-pixel remainder
/// carried across frames, X and Y combined into one move per frame. Driven with a
/// <see cref="FakeTimeSource"/> (1 s/step) against the capturing synthesizer.
/// </summary>
public sealed class MouseAxisRouteTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public MouseAxisRouteTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddAxis(Axis.Y).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	// One step = advance virtual time 1 s, then process. The first step only
	// establishes the time baseline (elapsed 0), so movement appears from step 2.
	private void Step(IFakesOutputRuntimeContext runtime)
	{
		_Time.Advance(TimeSpan.FromSeconds(1));
		runtime.ProcessFrame();
	}

	[Fact]
	public void Relative_MovesByValueTimesSensitivityTimesElapsed()
	{
		using var runtime = Build(
			_Stick.BindAxis(Axis.X).RouteToMouse(MouseDirection.X, MouseMovement.Relative, sensitivity: 1000));

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);                 // baseline frame (elapsed 0) → no move
		Assert.Empty(_Synth.Events);

		Step(runtime);                 // 1 s × 1.0 × 1000 = 1000 px
		var move = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.MouseMove, move.Kind);
		Assert.Equal(1000, move.Dx);
		Assert.Equal(0, move.Dy);
	}

	[Fact]
	public void Relative_AccumulatesSubPixelStepsAcrossFrames()
	{
		using var runtime = Build(
			_Stick.BindAxis(Axis.X).RouteToMouse(MouseDirection.X, MouseMovement.Relative, sensitivity: 1));

		_Stick.SetAxisValue(Axis.X, 0.6); // 0.6 px/frame
		Step(runtime);                    // baseline
		Step(runtime);                    // accumulator 0.6 → no whole pixel yet
		Assert.Empty(_Synth.Events);

		Step(runtime);                    // accumulator 1.2 → emit 1, keep 0.2
		var move = Assert.Single(_Synth.Events);
		Assert.Equal(1, move.Dx);
	}

	[Fact]
	public void XAndY_CombineIntoOneMovePerFrame()
	{
		using var runtime = Build(
			_Stick.BindAxis(Axis.X).RouteToMouse(MouseDirection.X, MouseMovement.Relative, sensitivity: 1000),
			_Stick.BindAxis(Axis.Y).RouteToMouse(MouseDirection.Y, MouseMovement.Relative, sensitivity: 500));

		_Stick.SetAxisValue(Axis.X, 1.0);
		_Stick.SetAxisValue(Axis.Y, 1.0);
		Step(runtime);
		Step(runtime);

		var move = Assert.Single(_Synth.Events);
		Assert.Equal(1000, move.Dx);
		Assert.Equal(500, move.Dy);
	}

	[Fact]
	public void AbsoluteMovement_NotYetImplemented_ThrowsAtBuild()
	{
		Assert.Throws<NotSupportedException>(() => Build(
			_Stick.BindAxis(Axis.X).RouteToMouse(MouseDirection.X, new MouseMovement { Kind = MovementKind.Absolute })));
	}

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			InputSynthesizer = _Synth,
			Routes = [..routes],
		});
}
