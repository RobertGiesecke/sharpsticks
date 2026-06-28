namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// Button→mouse routing: a held source holds the mouse button (down on the rising
/// edge, up on the falling edge, nothing while steady), and several sources on the
/// same button OR together — both must release before it lifts.
/// </summary>
public sealed class MouseButtonRouteTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public MouseButtonRouteTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void HeldSource_HoldsMouseButton_DownOnPress_UpOnRelease()
	{
		using var runtime = Build(_Stick.BindButton(1).RouteToMouse(OutputMouseButton.Left));

		runtime.ProcessFrame(); // baseline, not pressed
		Assert.Empty(_Synth.Events);

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		var down = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.MouseButtonDown, down.Kind);
		Assert.Equal(OutputMouseButton.Left, down.MouseButton);

		runtime.ProcessFrame(); // still held → no new event
		Assert.Single(_Synth.Events);

		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.Equal(2, _Synth.Events.Count);
		Assert.Equal(EventKind.MouseButtonUp, _Synth.Events[1].Kind);
	}

	[Fact]
	public void TwoSources_OnSameButton_BothMustReleaseBeforeItLifts()
	{
		using var runtime = Build(
			_Stick.BindButton(1).RouteToMouse(OutputMouseButton.Left),
			_Stick.BindButton(2).RouteToMouse(OutputMouseButton.Left));

		runtime.ProcessFrame();

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.Single(_Synth.Events); // down

		_Stick.PressButton(2);
		runtime.ProcessFrame();
		Assert.Single(_Synth.Events); // still down, no new event

		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.Single(_Synth.Events); // button 2 still holds it

		_Stick.ReleaseButton(2);
		runtime.ProcessFrame();
		Assert.Equal(2, _Synth.Events.Count);
		Assert.Equal(EventKind.MouseButtonUp, _Synth.Events[1].Kind);
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
