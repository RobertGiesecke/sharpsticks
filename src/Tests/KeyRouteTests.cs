namespace SharpSticks.Tests;

using SharpSticks.InputSynthesis.Keyboard;
using static FakeInputSynthesizer;

/// <summary>
/// Keyboard keys are just another <see cref="ButtonTarget"/>: a button holds a key while
/// pressed, and an axis zone holds a key while in range — both through the same unified
/// counter as vJoy/mouse, with no key-specific route type.
/// </summary>
public sealed class KeyRouteTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeInputSynthesizer _Synth = new();

	public KeyRouteTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Button_HoldsKey_DownOnPress_UpOnRelease()
	{
		using var runtime = Build(_Stick.BindButton(1).RouteToKey(NamedKey.A));

		runtime.ProcessFrame(); // baseline, not pressed
		Assert.Empty(_Synth.Events);

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		var down = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.KeyDown, down.Kind);
		Assert.Equal((Key)NamedKey.A, down.Key);

		runtime.ProcessFrame(); // still held → no new event
		Assert.Single(_Synth.Events);

		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.Equal(2, _Synth.Events.Count);
		Assert.Equal(EventKind.KeyUp, _Synth.Events[1].Kind);
	}

	[Fact]
	public void Zone_HoldsKey_WhileAxisInRange()
	{
		// Signed axis split into two key zones: [-1, 0) A, [0, 1] B.
		using var runtime = Build(
		[
			_Stick.BindAxis(Axis.X).SplitIntoButtons(
			[
				new KeyTarget(NamedKey.A),
				new KeyTarget(NamedKey.B),
			]),
		]);

		_Stick.SetAxisValue(Axis.X, -0.5);
		runtime.ProcessFrame();
		var down = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.KeyDown, down.Kind);
		Assert.Equal((Key)NamedKey.A, down.Key);

		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(3, _Synth.Events.Count);
		Assert.Equal(EventKind.KeyUp, _Synth.Events[1].Kind);   // A released
		Assert.Equal(EventKind.KeyDown, _Synth.Events[2].Kind); // B pressed
	}

	private IFakesOutputRuntimeContext Build(params IRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			InputSynthesizer = _Synth,
			Routes = [..routes],
		});
}
