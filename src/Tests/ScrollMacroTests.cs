namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// The scroll macro action drives the injected synthesizer directly (like the
/// key/mouse-button macro actions), emitting one scroll increment when the action runs.
/// </summary>
public sealed class ScrollMacroTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public ScrollMacroTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void OnPress_SynthesizesScrollIncrement()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [Macros.Scroll(ScrollDirection.Up, amount: 2)],
		});

		runtime.ProcessFrame(); // baseline: released edge, no events
		_Stick.PressButton(1);
		runtime.ProcessFrame();

		var ev = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.Scroll, ev.Kind);
		Assert.Equal(2, ev.Dy);  // vertical, up
		Assert.Equal(0, ev.Dx);
		Assert.Equal(MouseScrollUnit.Notch, ev.Unit);
	}

	private IFakesOutputRuntimeContext Build(params IBoundRoute[] routes) =>
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
