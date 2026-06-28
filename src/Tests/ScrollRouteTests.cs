namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// Scroll routing. Button→scroll pulses one increment on the rising edge (no repeat
/// while held); axis→scroll integrates the normalized axis as scroll speed
/// (notches/sec at full deflection × elapsed), carrying a sub-step remainder.
/// In the fake synthesizer a scroll event carries vertical in <c>Dy</c>, horizontal
/// in <c>Dx</c>, and the unit in <c>Unit</c>.
/// </summary>
public sealed class ScrollRouteTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public ScrollRouteTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	private void Step(IFakesOutputRuntimeContext runtime)
	{
		_Time.Advance(TimeSpan.FromSeconds(1));
		runtime.ProcessFrame();
	}

	[Fact]
	public void Button_PulsesOnceOnRisingEdge_NoRepeatWhileHeld()
	{
		using var runtime = Build(_Stick.BindButton(1).RouteToScroll(ScrollDirection.Up));

		runtime.ProcessFrame(); // baseline, not pressed
		Assert.Empty(_Synth.Events);

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		var pulse = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.Scroll, pulse.Kind);
		Assert.Equal(1, pulse.Dy);  // vertical, up
		Assert.Equal(0, pulse.Dx);
		Assert.Equal(MouseScrollUnit.Notch, pulse.Unit);

		runtime.ProcessFrame(); // still held → no repeat
		Assert.Single(_Synth.Events);
	}

	[Fact]
	public void Button_RepulsesAfterReleaseAndPress()
	{
		using var runtime = Build(_Stick.BindButton(1).RouteToScroll(ScrollDirection.Up));

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();

		Assert.Equal(2, _Synth.Events.Count);
		Assert.All(_Synth.Events, e => Assert.Equal(EventKind.Scroll, e.Kind));
	}

	[Theory]
	[InlineData(ScrollDirection.Up, 0, 1)]
	[InlineData(ScrollDirection.Down, 0, -1)]
	[InlineData(ScrollDirection.Right, 1, 0)]
	[InlineData(ScrollDirection.Left, -1, 0)]
	public void Button_DirectionMapsToSignedAxis(ScrollDirection direction, int expectedHorizontal, int expectedVertical)
	{
		using var runtime = Build(_Stick.BindButton(1).RouteToScroll(direction));

		_Stick.PressButton(1);
		runtime.ProcessFrame();

		var pulse = Assert.Single(_Synth.Events);
		Assert.Equal(expectedHorizontal, pulse.Dx);
		Assert.Equal(expectedVertical, pulse.Dy);
	}

	[Fact]
	public void Button_HonorsAmountAndUnit()
	{
		using var runtime = Build(
			_Stick.BindButton(1).RouteToScroll(ScrollDirection.Up, amount: 3, unit: MouseScrollUnit.HighResolution));

		_Stick.PressButton(1);
		runtime.ProcessFrame();

		var pulse = Assert.Single(_Synth.Events);
		Assert.Equal(3, pulse.Dy);
		Assert.Equal(MouseScrollUnit.HighResolution, pulse.Unit);
	}

	[Fact]
	public void Axis_ScrollsByValueTimesSensitivityTimesElapsed()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteToScroll(ScrollAxis.Vertical, sensitivity: 10));

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);                 // baseline frame (elapsed 0) → no scroll
		Assert.Empty(_Synth.Events);

		Step(runtime);                 // 1 s × 1.0 × 10 = 10 notches up
		var scroll = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.Scroll, scroll.Kind);
		Assert.Equal(10, scroll.Dy);
		Assert.Equal(0, scroll.Dx);
		Assert.Equal(MouseScrollUnit.Notch, scroll.Unit);
	}

	[Fact]
	public void Axis_AccumulatesSubNotchStepsAcrossFrames()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteToScroll(ScrollAxis.Vertical, sensitivity: 1));

		_Stick.SetAxisValue(Axis.X, 0.6); // 0.6 notch/frame
		Step(runtime);                    // baseline
		Step(runtime);                    // accumulator 0.6 → no whole notch yet
		Assert.Empty(_Synth.Events);

		Step(runtime);                    // accumulator 1.2 → emit 1, keep 0.2
		var scroll = Assert.Single(_Synth.Events);
		Assert.Equal(1, scroll.Dy);
	}

	[Fact]
	public void Axis_HighResolution_ScalesByHundredTwenty()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X)
			.RouteToScroll(ScrollAxis.Vertical, unit: MouseScrollUnit.HighResolution, sensitivity: 1));

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);                 // baseline
		Step(runtime);                 // 1 notch/sec × 120 hi-res/notch = 120 hi-res units

		var scroll = Assert.Single(_Synth.Events);
		Assert.Equal(120, scroll.Dy);
		Assert.Equal(MouseScrollUnit.HighResolution, scroll.Unit);
	}

	[Fact]
	public void Axis_Horizontal_DrivesHorizontalWheel()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteToScroll(ScrollAxis.Horizontal, sensitivity: 10));

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Step(runtime);

		var scroll = Assert.Single(_Synth.Events);
		Assert.Equal(10, scroll.Dx);
		Assert.Equal(0, scroll.Dy);
	}

	private IFakesOutputRuntimeContext Build(params IRoute[] routes) =>
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
