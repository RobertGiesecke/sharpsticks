namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// The mouse-move macro action drives the synthesizer's relative move directly, like the
/// key/mouse-button/scroll actions.
/// </summary>
public sealed class MouseMoveMacroTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeInputSynthesizer _Synth = new();

	public MouseMoveMacroTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void OnPress_SynthesizesRelativeMove()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [Macros.MoveMouse(10, -5)],
		});

		runtime.ProcessFrame(); // baseline: released edge, no events
		_Stick.PressButton(1);
		runtime.ProcessFrame();

		var ev = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.MouseMove, ev.Kind);
		Assert.Equal(10, ev.Dx);
		Assert.Equal(-5, ev.Dy);
	}

	private IFakesOutputRuntimeContext Build(params IBoundRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			InputSynthesizer = _Synth,
			Routes = [..routes],
		});
}
