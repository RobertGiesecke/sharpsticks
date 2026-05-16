namespace ScaledAxisCSharp.Tests;

public sealed class WhenButtonPressedAxisModifierTests
{
	private const double Precision = 1e-9;

	[Fact]
	public void StatefulWhenPressed_HoldsCurrentOutputOnPress_AndIntegratesViaPrecisionCurve()
	{
		// Mirrors the hold-on-press scenario from stateful-axis-zoom.cs.
		// WhenPressed = 0.5x precision curve, WhenNotPressed = identity.
		using var fakes = new FakeDeviceManager();
		const int button = 1;

		var stick = fakes
			.AddInputDevice("FakeStick")
			.AddAxis(Axis.X)
			.AddButtons(4)
			.Build();

		var output = fakes.AddOutputDevice()
			.AddAxis(Axis.X)
			.Build();

		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [stick.BindButton(button)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.WhenPressed,
		};

		using var runtime = Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = fakes.InputDevices,
			OutputDeviceFactory = fakes.OutputDeviceFactory,
			Routes =
			[
				stick
					.BindAxis(Axis.X)
					.RouteToSameAxisOnOutput(output, modifier: modifier),
			],
		});

		// Frame 1: trigger off, axis at 0.8 → identity curve, output = 0.8.
		stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, output.GetAxisValue(Axis.X), Precision);

		// Frame 2: press trigger; axis still at 0.8.
		// The stateful branch activates, but the output must STAY at 0.8 —
		// only future input changes should run through the precision curve.
		stick.PressButton(button);
		runtime.ProcessFrame();
		Assert.Equal(0.8, output.GetAxisValue(Axis.X), Precision);

		// Frame 3: button still held, axis moves to 0.6.
		// Output drifts by the precision-curve delta only:
		// delta = precision(0.6) - precision(0.8) = 0.3 - 0.4 = -0.1
		// new output = 0.8 + (-0.1) = 0.7
		stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, output.GetAxisValue(Axis.X), Precision);

		// Frame 4: button still held, axis moves to 0.4.
		// delta = precision(0.4) - precision(0.6) = 0.2 - 0.3 = -0.1
		// new output = 0.7 + (-0.1) = 0.6
		stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.Equal(0.6, output.GetAxisValue(Axis.X), Precision);

		// Frame 5: release trigger, axis still 0.4. We accept the jump back
		// to the non-stateful branch's identity curve.
		stick.ReleaseButton(button);
		runtime.ProcessFrame();
		Assert.Equal(0.4, output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void StatefulNone_AlwaysPassesThrough()
	{
		const int button = 1;
		using var fakes = new FakeDeviceManager();

		var stick = fakes.AddInputDevice("FakeStick")
			.AddAxis(Axis.X)
			.AddButtons(4)
			.Build();

		var output = fakes.AddOutputDevice()
			.AddAxis(Axis.X)
			.Build();

		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [stick.BindButton(button)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
			Stateful = WhenButtonPressedStateful.None,
		};

		using var runtime = Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = fakes.InputDevices,
			OutputDeviceFactory = fakes.OutputDeviceFactory,
			Routes =
			[
				stick
					.BindAxis(Axis.X)
					.RouteToSameAxisOnOutput(output, modifier: modifier),
			],
		});

		stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, output.GetAxisValue(Axis.X), Precision);

		// Pressing the button must produce an immediate half-scale jump
		// because the stateful behavior is disabled.
		stick.PressButton(button);
		runtime.ProcessFrame();
		Assert.Equal(0.4, output.GetAxisValue(Axis.X), Precision);
	}
}