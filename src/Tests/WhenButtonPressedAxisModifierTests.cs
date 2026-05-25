namespace SharpSticks.Tests;

public sealed class WhenButtonPressedAxisModifierTests : IDisposable
{
	private const double Precision = 1e-9;
	private const int Button1 = 1;
	private const int Button2 = 2;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public WhenButtonPressedAxisModifierTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void StatefulWhenPressed_HoldsCurrentOutputOnPress_AndIntegratesViaPrecisionCurve()
	{
		// Mirrors the hold-on-press scenario from stateful-axis-zoom.cs.
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.WhenPressed,
		};
		using var runtime = BuildRuntime(modifier);

		// Frame 1: trigger off, axis at 0.8 → identity curve, output = 0.8.
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Frame 2: press trigger; axis still at 0.8.
		// The stateful branch activates, but the output must STAY at 0.8.
		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Frame 3: axis 0.8 → 0.6 while held.
		// delta = precision(0.6) - precision(0.8) = 0.3 - 0.4 = -0.1
		// new output = 0.8 + (-0.1) = 0.7
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Frame 4: axis 0.6 → 0.4. Output 0.6.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);

		// Frame 5: release. Jump back to the non-stateful identity curve.
		_Stick.ReleaseButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void StatefulNone_AlwaysPassesThrough()
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.None,
		};
		using var runtime = BuildRuntime(modifier);

		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Pressing the button produces an immediate half-scale jump.
		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void StatefulWhenNotPressed_HoldsOnRelease_AndIntegratesViaNonPressedBranch()
	{
		// Inverse of WhenPressed: the not-pressed branch latches.
		// WhenPressed = identity, WhenNotPressed = 0.5x precision curve.
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 1.0 },
			WhenNotPressed = new AxisCurve { Max = 0.5 },
			Stateful = WhenButtonPressedStateful.WhenNotPressed,
		};
		using var runtime = BuildRuntime(modifier);

		// Press the button so we start in the non-stateful (pressed) branch.
		_Stick.PressButton(Button1);
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Release the button — stateful branch activates, output HOLDS.
		_Stick.ReleaseButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Drift input through the 0.5x curve while released.
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void StatefulBoth_IntegratesAcrossBranchTransitions_NoJumps()
	{
		// With Both, swapping branches doesn't reseed state — the integrator
		// just switches which curve it uses for delta computation.
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.Both,
		};
		using var runtime = BuildRuntime(modifier);

		// Seed: axis 0.8, not pressed, output 0.8.
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Press → no jump (state was active in not-pressed too).
		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Move input while pressed: delta = precision(0.6) - precision(0.8) = -0.1.
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Release: branch switches to identity, no jump.
		_Stick.ReleaseButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Move input while released: delta = identity(0.4) - identity(0.6) = -0.2.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void WhenPressedNull_ButtonPressed_FallsThroughToWhenNotPressed()
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = null,
			WhenNotPressed = new AxisCurve { Max = 0.5 },
			Stateful = WhenButtonPressedStateful.None,
		};
		using var runtime = BuildRuntime(modifier);

		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		// WhenPressed is null → active modifier is WhenNotPressed (0.5x).
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void WhenNotPressedNull_ButtonReleased_ReturnsInputAsIs()
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = null,
			Stateful = WhenButtonPressedStateful.None,
		};
		using var runtime = BuildRuntime(modifier);

		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		// No modifier active when released → raw input.
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void BothBranchesNull_AlwaysReturnsInputAsIs()
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = null,
			WhenNotPressed = null,
			Stateful = WhenButtonPressedStateful.None,
		};
		using var runtime = BuildRuntime(modifier);

		_Stick.SetAxisValue(Axis.X, 0.7);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void MultipleButtons_AnyPressed_ActivatesWhenPressed()
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1), _Stick.BindButton(Button2)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.None,
		};
		using var runtime = BuildRuntime(modifier);

		_Stick.SetAxisValue(Axis.X, 1.0);

		// Neither pressed → WhenNotPressed (identity).
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.X), Precision);

		// Press Button1 → WhenPressed (0.5x).
		_Stick.PressButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);

		// Press Button2 too (still pressed).
		_Stick.PressButton(Button2);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);

		// Release Button1, Button2 still pressed → still WhenPressed.
		_Stick.ReleaseButton(Button1);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);

		// Release Button2 too → back to WhenNotPressed.
		_Stick.ReleaseButton(Button2);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.X), Precision);
	}

	private IFakesOutputRuntimeContext BuildRuntime(WhenButtonPressedAxisModifier modifier) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteToSameAxisOnOutput(_Output, modifier: modifier),
			],
		});
}
