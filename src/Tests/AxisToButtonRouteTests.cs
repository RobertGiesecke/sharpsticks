namespace SharpSticks.Tests;

public sealed class AxisToButtonRouteTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;
	private readonly FakeTimeSource _Time = new();

	public AxisToButtonRouteTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Output = _Fakes.AddOutputDevice().AddButtons(8).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Hold_PressesWhileInRange_ReleasesWhenLeaving()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.3, 0.6, _Output.BindButton(1)));

		// Outside range -> not pressed.
		_Stick.SetAxisValue(Axis.X, 0.1);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(1));

		// Enter range -> pressed.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));

		// Still in range -> still pressed.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));

		// Leave range -> released.
		_Stick.SetAxisValue(Axis.X, 0.7);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(1));
	}

	[Fact]
	public void Hold_ClosedBoundaries_BothEndpointsAssert()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.25, 0.5, _Output.BindButton(1)));

		_Stick.SetAxisValue(Axis.X, 0.25);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));

		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));
	}

	[Fact]
	public void Hold_HalfOpenBoundary_ExcludesMax()
	{
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.25, 0.5, _Output.BindButton(1),
			new() { IncludeMax = false }));

		_Stick.SetAxisValue(Axis.X, 0.25);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));

		// Exactly Max is excluded with IncludeMax=false.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(1));
	}

	[Fact]
	public void Pulse_PressesOnEntry_ReleasesAfterDuration_ReArmsAfterLeaving()
	{
		var pulse = TimeSpan.FromMilliseconds(50);
		using var runtime = Build(_Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.3, 0.6, _Output.BindButton(2),
			new() { Mode = AxisZoneTriggerMode.Pulse, PulseDuration = pulse }));

		// Enter range -> press.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(2));

		// Half-duration later: still pressed.
		_Time.Advance(TimeSpan.FromMilliseconds(25));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(2));

		// Past duration: released even though axis still in range.
		_Time.Advance(TimeSpan.FromMilliseconds(30));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(2));

		// Still in range -> stays released (no auto-re-pulse).
		_Time.Advance(TimeSpan.FromMilliseconds(100));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(2));

		// Leave range -> still released.
		_Stick.SetAxisValue(Axis.X, 0.1);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(2));

		// Re-enter -> new pulse fires.
		_Stick.SetAxisValue(Axis.X, 0.45);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(2));
	}

	[Fact]
	public void SplitIntoButtons_Unsigned_FourEqualZones()
	{
		var stick = _Fakes.AddInputDevice("Pedal")
			.AddAxis(Axis.Z)
			.Build();
		var binding = new AxisBinding(stick.DeviceId, Axis.Z, AxisMode.Unsigned);

		var b1 = _Output.BindButton(1);
		var b2 = _Output.BindButton(2);
		var b3 = _Output.BindButton(3);
		var b4 = _Output.BindButton(4);

		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			Routes = [binding.SplitIntoButtons([b1, b2, b3, b4])],
		});

		// Zone 1: [0, 0.25)
		stick.SetAxisValue(Axis.Z, 0.1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));
		Assert.False(_Output.GetButtonState(2));

		// Boundary 0.25 belongs to zone 2 (half-open lower zone).
		stick.SetAxisValue(Axis.Z, 0.25);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(1));
		Assert.True(_Output.GetButtonState(2));

		// Zone 3 mid: 0.6
		stick.SetAxisValue(Axis.Z, 0.6);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Top of range belongs to last zone (closed on max).
		stick.SetAxisValue(Axis.Z, 1.0);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(4));
	}

	[Fact]
	public void SplitIntoButtons_Signed_DerivesRangeFromMode()
	{
		// Signed axis: range is [-1, 1]. Two buttons -> [-1, 0) and [0, 1].
		using var runtime = Build([_Stick.BindAxis(Axis.X).SplitIntoButtons(
			[_Output.BindButton(5), _Output.BindButton(6)])]);

		_Stick.SetAxisValue(Axis.X, -0.5);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(5));
		Assert.False(_Output.GetButtonState(6));

		// 0.0 belongs to zone 2 (half-open boundary).
		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(5));
		Assert.True(_Output.GetButtonState(6));

		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(6));
	}

	[Fact]
	public void Composition_AxisZone_OrsWithButtonRoute()
	{
		// Same output button receives both a regular ButtonRoute and an axis-zone source.
		var target = _Output.BindButton(7);
		using var runtime = Build(
			_Stick.BindButton(1).RouteTo(target),
			_Stick.BindAxis(Axis.X).RouteWhenInRange(0.5, 1.0, target));

		// Neither asserting.
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(7));

		// Button asserts.
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(7));

		// Release button, but axis-zone asserts.
		_Stick.ReleaseButton(1);
		_Stick.SetAxisValue(Axis.X, 0.7);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(7));

		// Both off.
		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(7));
	}

	[Fact]
	public void RouteZones_ExplicitList_AssertsCorrectButton()
	{
		using var runtime = Build([.._Stick.BindAxis(Axis.X).RouteZones(
		[
			new(-1.0, -0.5, _Output.BindButton(1)),
			new(0.5, 1.0, _Output.BindButton(2)),
		])]);

		_Stick.SetAxisValue(Axis.X, -0.8);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(1));
		Assert.False(_Output.GetButtonState(2));

		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(1));
		Assert.False(_Output.GetButtonState(2));

		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(2));
	}

	[Fact]
	public void Build_RejectsMaxLessThanMin()
	{
		var route = _Stick.BindAxis(Axis.X).RouteWhenInRange(
			0.5, 0.3, _Output.BindButton(1));

		var ex = Assert.Throws<InvalidOperationException>(() =>
			FakesRuntime.Build(new()
			{
				Name = "test",
				ConnectedDevices = _Fakes.InputDevices,
				OutputDeviceFactory = _Fakes.OutputDeviceFactory,
				TimeSource = _Time,
				Routes = [route],
			}));
		Assert.Contains("Max", ex.Message);
	}

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			Routes = [..routes],
		});
}
