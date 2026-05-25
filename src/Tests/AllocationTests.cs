namespace SharpSticks.Tests;

/// <summary>
/// Verifies that the runtime's per-frame hot path does not allocate on the
/// managed heap once the runtime has been built. The comprehensive runtime
/// exercises every modifier (AxisCurve, BlendedAxisCurve stateful,
/// WhenButtonPressedAxisModifier stateful, MergeAxesModifier,
/// AbsoluteRelativeAxisModifier), every route type (ButtonRoute, AxisRoute,
/// ButtonMacroRoute, AxisToButtonRoute Hold + Pulse, SplitIntoButtons), and
/// two input + two output devices. Allocations are measured via
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> after a warm-up pass.
/// </summary>
public sealed class AllocationTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick1;
	private readonly FakeJoystickDevice _Stick2;
	private readonly FakeOutputDevice _Output1;
	private readonly FakeOutputDevice _Output2;
	private readonly FakeTimeSource _Time = new();

	public AllocationTests()
	{
		_Stick1 = _Fakes.AddInputDevice("Stick1")
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddAxis(Axis.Z).AddAxis(Axis.Slider1)
			.AddButtons(8)
			.Build();
		_Stick2 = _Fakes.AddInputDevice("Stick2")
			.AddAxis(Axis.X).AddAxis(Axis.Y)
			.AddButtons(4)
			.Build();
		_Output1 = _Fakes.AddOutputDevice()
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddAxis(Axis.Z).AddAxis(Axis.Rx)
			.AddAxis(Axis.Slider1).AddAxis(Axis.Slider2)
			.AddButtons(8)
			.Build();
		_Output2 = _Fakes.AddOutputDevice()
			.AddButtons(8)
			.Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void ProcessFrame_SteadyState_DoesNotAllocate()
	{
		using var runtime = BuildComprehensiveRuntime();
		PopulateState();

		WarmUp(runtime, iterations: 50);

		var allocated = MeasureAllocations(() =>
		{
			for (var i = 0; i < 200; i++)
			{
				runtime.ProcessFrame();
			}
		});

		Assert.Equal(0, allocated);
	}

	[Fact]
	public void ProcessFrame_WithTransitions_DoesNotAllocate()
	{
		using var runtime = BuildComprehensiveRuntime();
		WarmUp(runtime, iterations: 50);

		var allocated = MeasureAllocations(() =>
		{
			for (var i = 0; i < 200; i++)
			{
				var phase = i % 4;
				// Cycle all source axes so every modifier sees movement, every
				// AxisToButton zone gets enter/exit, MergeAxes sees both sides,
				// AbsoluteRelative sees its target sweep, and BlendedAxisCurve's
				// modifier-axis (Slider1) drives the blend.
				_Stick1.SetAxisValue(Axis.X, phase switch { 0 => 0.1, 1 => 0.45, 2 => 0.55, _ => 0.85 });
				_Stick1.SetAxisValue(Axis.Y, phase switch { 0 => 0.2, 1 => 0.5, 2 => 0.5, _ => 0.9 });
				_Stick1.SetAxisValue(Axis.Z, phase switch { 0 => -0.5, 1 => 0.0, 2 => 0.3, _ => 0.7 });
				_Stick1.SetAxisValue(Axis.Slider1, phase switch { 0 => 0.0, 1 => 0.3, 2 => 0.6, _ => 0.9 });
				_Stick2.SetAxisValue(Axis.X, phase switch { 0 => -0.8, 1 => -0.2, 2 => 0.3, _ => 0.9 });
				_Stick2.SetAxisValue(Axis.Y, phase switch { 0 => 0.0, 1 => 0.4, 2 => 0.6, _ => 1.0 });

				// Toggle every source button so we hit:
				// - ButtonRoute edges (1, 2 on Stick1; 1 on Stick2)
				// - WhenButtonPressedAxisModifier branch switching (Stick1 button 1)
				// - ButtonMacroRoute press/release edges (Stick1 button 3) — rents from
				//   the pre-allocated session pool, so no MacroSession allocations.
				if ((i & 1) == 0)
				{
					_Stick1.PressButton(1); _Stick1.PressButton(2); _Stick1.PressButton(3);
					_Stick2.PressButton(1);
				}
				else
				{
					_Stick1.ReleaseButton(1); _Stick1.ReleaseButton(2); _Stick1.ReleaseButton(3);
					_Stick2.ReleaseButton(1);
				}

				_Time.Advance(TimeSpan.FromMilliseconds(20));
				runtime.ProcessFrame();
			}
		});

		Assert.Equal(0, allocated);
	}

	private static long MeasureAllocations(Action action)
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var before = GC.GetAllocatedBytesForCurrentThread();
		action();
		var after = GC.GetAllocatedBytesForCurrentThread();
		return after - before;
	}

	private void PopulateState()
	{
		_Stick1.SetAxisValue(Axis.X, 0.4);
		_Stick1.SetAxisValue(Axis.Y, 0.4);
		_Stick1.SetAxisValue(Axis.Z, 0.2);
		_Stick1.SetAxisValue(Axis.Slider1, 0.5);
		_Stick2.SetAxisValue(Axis.X, 0.3);
		_Stick2.SetAxisValue(Axis.Y, 0.5);
	}

	private void WarmUp(IFakesOutputRuntimeContext runtime, int iterations)
	{
		for (var i = 0; i < iterations; i++)
		{
			_Stick1.PressButton(1); _Stick1.PressButton(2); _Stick2.PressButton(1);
			_Stick1.SetAxisValue(Axis.X, 0.45); _Stick1.SetAxisValue(Axis.Y, 0.5);
			_Stick1.SetAxisValue(Axis.Z, 0.3); _Stick1.SetAxisValue(Axis.Slider1, 0.6);
			_Stick2.SetAxisValue(Axis.X, -0.3); _Stick2.SetAxisValue(Axis.Y, 0.4);
			_Time.Advance(TimeSpan.FromMilliseconds(20));
			runtime.ProcessFrame();

			_Stick1.ReleaseButton(1); _Stick1.ReleaseButton(2); _Stick2.ReleaseButton(1);
			_Stick1.SetAxisValue(Axis.X, 0.1); _Stick1.SetAxisValue(Axis.Y, 0.1);
			_Stick1.SetAxisValue(Axis.Z, -0.4); _Stick1.SetAxisValue(Axis.Slider1, 0.0);
			_Stick2.SetAxisValue(Axis.X, 0.8); _Stick2.SetAxisValue(Axis.Y, 0.9);
			_Time.Advance(TimeSpan.FromMilliseconds(100));
			runtime.ProcessFrame();
		}
	}

	private IFakesOutputRuntimeContext BuildComprehensiveRuntime()
	{
		var blended = new BlendedAxisCurve
		{
			NormalCurve = new AxisCurve { Max = 1.0 },
			PrecisionCurve = new AxisCurve { Max = 0.5 },
			ModifierAxis = _Stick1.BindAxis(Axis.Slider1),
			Stateful = true,
		};

		var whenPressed = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick1.BindButton(1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.WhenPressed,
		};

		var absrel = new AbsoluteRelativeAxisOptions
		{
			IncreaseAxis = _Output1.BindAxis(Axis.Slider1),
			DecreaseAxis = _Output1.BindAxis(Axis.Slider2),
			Minimum = 0.0,
			Maximum = 1.0,
			InitialValue = 0.5,
		};

		return FakesRuntime.Build(new()
		{
			Name = "alloc-comprehensive",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			Routes =
			[
				// AxisRoute with AxisCurve (non-linear)
				_Stick1.BindAxis(Axis.X).RouteToSameAxisOnOutput(
					_Output1, modifier: new AxisCurve { Max = 0.8, Steepness = 1.5 }),

				// AxisRoute with WhenButtonPressedAxisModifier (stateful branch)
				_Stick1.BindAxis(Axis.Y).RouteToSameAxisOnOutput(_Output1, modifier: whenPressed),

				// AxisRoute with BlendedAxisCurve (stateful, reads modifier axis)
				_Stick1.BindAxis(Axis.Z).RouteToSameAxisOnOutput(_Output1, modifier: blended),

				// MergeAxesModifier across two input devices
				_Stick1.BindAxis(Axis.X).MergeWith(
					_Stick2.BindAxis(Axis.X),
					new()
					{
						OutputBinding = _Output1.BindAxis(Axis.Rx),
						Mode = MergeMode.Average,
					}),

				// AbsoluteRelativeAxisModifier (2 routes sharing state)
				.._Stick2.BindAxis(Axis.Y).RouteAbsoluteRelative(absrel),

				// ButtonRoutes from both input devices
				_Stick1.BindButton(1).RouteTo(_Output1.BindButton(1)),
				_Stick1.BindButton(2).RouteTo(_Output1.BindButton(2)),
				_Stick2.BindButton(1).RouteTo(_Output1.BindButton(3)),

				// AxisToButtonRoute (Hold + Pulse)
				_Stick1.BindAxis(Axis.X).RouteWhenInRange(0.3, 0.6, _Output2.BindButton(1)),
				_Stick1.BindAxis(Axis.Y).RouteWhenInRange(0.3, 0.6, _Output2.BindButton(2),
					new()
					{
						Mode = AxisZoneTriggerMode.Pulse,
						PulseDuration = TimeSpan.FromMilliseconds(50),
					}),

				// SplitIntoButtons (4 even zones on a signed axis)
				.._Stick2.BindAxis(Axis.X).SplitIntoButtons(
				[
					_Output2.BindButton(3), _Output2.BindButton(4),
					_Output2.BindButton(5), _Output2.BindButton(6),
				]),

				// ButtonMacroRoute (engine + Press/Release actions)
				_Stick1.BindButton(3).ComplexRoute(new()
				{
					OnPress = [_Output2.BindButton(7).Press()],
					OnRelease = [_Output2.BindButton(7).Release()],
				}),
			],
		});
	}
}
