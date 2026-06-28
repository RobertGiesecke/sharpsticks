namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// <c>SplitIntoButtons</c> with a <see cref="MouseButtonTarget"/> holds the mouse
/// button while the axis is in that zone, and ORs with any physical button routed to
/// the same mouse button (either source holds it).
/// </summary>
public sealed class AxisZoneToMouseButtonTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeInputSynthesizer _Synth = new();
	private readonly FakeTimeSource _Time = new();

	public AxisZoneToMouseButtonTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Zone_HoldsMouseButton_WhileAxisInRange()
	{
		// Signed axis split into two mouse-button zones: [-1, 0) Left, [0, 1] Right.
		using var runtime = Build(
		[
			_Stick.BindAxis(Axis.X).SplitIntoButtons(
			[
				new MouseButtonTarget { Button = OutputMouseButton.Left },
				new MouseButtonTarget { Button = OutputMouseButton.Right },
			]),
		]);

		_Stick.SetAxisValue(Axis.X, -0.5);
		runtime.ProcessFrame();
		var down = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.MouseButtonDown, down.Kind);
		Assert.Equal(OutputMouseButton.Left, down.MouseButton);

		runtime.ProcessFrame(); // still in the Left zone → no new event
		Assert.Single(_Synth.Events);

		// Cross into the Right zone: Left releases, Right presses.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(3, _Synth.Events.Count);
		Assert.Equal(EventKind.MouseButtonUp, _Synth.Events[1].Kind);
		Assert.Equal(OutputMouseButton.Left, _Synth.Events[1].MouseButton);
		Assert.Equal(EventKind.MouseButtonDown, _Synth.Events[2].Kind);
		Assert.Equal(OutputMouseButton.Right, _Synth.Events[2].MouseButton);
	}

	[Fact]
	public void Zone_OrsWithPhysicalButton_OnSameMouseButton()
	{
		// Right is driven by both a physical button and the upper axis zone [0, 1].
		using var runtime = Build(
		[
			_Stick.BindButton(1).RouteToMouse(OutputMouseButton.Right),
			_Stick.BindAxis(Axis.X).SplitIntoButtons(
			[
				new MouseButtonTarget { Button = OutputMouseButton.Left },
				new MouseButtonTarget { Button = OutputMouseButton.Right },
			]),
		]);

		_Stick.SetAxisValue(Axis.X, -0.5); // Left zone active, Right idle
		runtime.ProcessFrame();

		_Stick.PressButton(1);             // physical press holds Right down
		runtime.ProcessFrame();
		Assert.Contains(_Synth.Events, e =>
			e.Kind == EventKind.MouseButtonDown && e.MouseButton == OutputMouseButton.Right);

		// Release the button but move into the Right zone: the zone keeps Right held.
		_Stick.ReleaseButton(1);
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.DoesNotContain(_Synth.Events, e =>
			e.Kind == EventKind.MouseButtonUp && e.MouseButton == OutputMouseButton.Right);
	}

	[Fact]
	public void Zone_PulseMode_PulsesOnceOnEntering_ThenWaits()
	{
		using var runtime = Build(
		[
			_Stick.BindAxis(Axis.X).SplitIntoButtons(
			[
				new MouseButtonTarget { Button = OutputMouseButton.Left },
				new MouseButtonTarget { Button = OutputMouseButton.Right },
			],
			new() { Mode = AxisZoneTriggerMode.Pulse, PulseDuration = TimeSpan.FromMilliseconds(50) }),
		]);

		_Stick.SetAxisValue(Axis.X, -0.5); // enter the Left zone
		runtime.ProcessFrame();            // rising edge → pulse down
		_Time.Advance(TimeSpan.FromMilliseconds(10));
		runtime.ProcessFrame();            // still within the pulse window → no change
		_Time.Advance(TimeSpan.FromMilliseconds(60));
		runtime.ProcessFrame();            // pulse elapsed → up

		var left = _Synth.Events.Where(e => e.MouseButton == OutputMouseButton.Left).ToArray();
		Assert.Equal(2, left.Length);
		Assert.Equal(EventKind.MouseButtonDown, left[0].Kind);
		Assert.Equal(EventKind.MouseButtonUp, left[1].Kind);
		Assert.DoesNotContain(_Synth.Events, e => e.MouseButton == OutputMouseButton.Right);
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
