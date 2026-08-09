namespace SharpSticks.Tests;

/// <summary>
/// The script shape: one <see cref="BlendedAxisCurve"/> instance shared by the
/// roll/pitch/yaw routes. Each route must get its own latched state — engaging
/// the lever with every input steady must not move any output, no matter where
/// the individual inputs happen to rest.
/// </summary>
public sealed class BlendedAxisCurveSharedInstanceTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public BlendedAxisCurveSharedInstanceTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X)
			.AddAxis(Axis.Y)
			.AddAxis(Axis.Z)
			.AddAxis(Axis.Slider1)
			.Build();

		_Output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.X)
			.AddAxis(Axis.Y)
			.AddAxis(Axis.Z)
			.Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Stateful_EngagingLever_WithSteadyInputs_HoldsAllOutputs()
	{
		// Profile-like curves: pronounced normal exponent, tiny precision max.
		using var runtime = BuildThreeRoutes(new()
		{
			NormalCurve = new AxisCurve { Max = 1.0, Exponent = 2.4 },
			PrecisionCurve = new AxisCurve { Max = 0.05 },
			ModifierAxes = [_Stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned }],
			Stateful = true,
		});

		// Roll centered exactly; pitch and twist resting slightly deflected,
		// symmetric — the physical situation on a real stick.
		_Stick.SetAxisValue(Axis.Y, -0.1);
		_Stick.SetAxisValue(Axis.Z, 0.1);
		runtime.ProcessWithDefaultFrameTime();
		var pitchAtRest = _Output.GetAxisValue(Axis.Y);
		var twistAtRest = _Output.GetAxisValue(Axis.Z);

		// Pull the lever gradually; no input moves.
		foreach (var lever in (double[])[0.1, 0.25, 0.5, 0.75, 1.0])
		{
			_Stick.SetAxisValue(Axis.Slider1, lever);
			runtime.ProcessWithDefaultFrameTime();
			Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);
			Assert.Equal(pitchAtRest, _Output.GetAxisValue(Axis.Y), Precision);
			Assert.Equal(twistAtRest, _Output.GetAxisValue(Axis.Z), Precision);
		}

		// And with several frames at full pull (continuous ticks re-run routes).
		for (var i = 0; i < 10; i++)
		{
			runtime.ProcessWithDefaultFrameTime();
		}

		Assert.Equal(pitchAtRest, _Output.GetAxisValue(Axis.Y), Precision);
		Assert.Equal(twistAtRest, _Output.GetAxisValue(Axis.Z), Precision);
	}

	[Fact]
	public void Stateful_MovingOneAxisWhileEngaged_DoesNotDisturbTheOthers()
	{
		using var runtime = BuildThreeRoutes(new()
		{
			NormalCurve = new AxisCurve { Max = 1.0 },
			PrecisionCurve = new AxisCurve { Max = 0.5 },
			ModifierAxes = [_Stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned }],
			Stateful = true,
		});

		_Stick.SetAxisValue(Axis.Y, 0.4);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessWithDefaultFrameTime();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.Y), Precision);

		// Twist around while engaged: only yaw may move.
		_Stick.SetAxisValue(Axis.Z, 0.6);
		runtime.ProcessWithDefaultFrameTime();
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Z), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.Y), Precision);

		_Stick.SetAxisValue(Axis.Z, 0.0);
		runtime.ProcessWithDefaultFrameTime();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Z), Precision);
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.Y), Precision);
	}

	[Fact]
	public void NonStateful_EngagingLever_ShiftsOffCenterOutputsTowardPrecisionCurve()
	{
		// The reported symptom's signature: without Stateful, pulling the lever
		// slides every off-center output from normal(u) toward precision(u) —
		// equal and opposite for symmetric rest offsets, invisible at exact 0.
		using var runtime = BuildThreeRoutes(new()
		{
			NormalCurve = new AxisCurve { Max = 1.0, Exponent = 2.4 },
			PrecisionCurve = new AxisCurve { Max = 0.05 },
			ModifierAxes = [_Stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned }],
			Stateful = false,
		});

		_Stick.SetAxisValue(Axis.Y, -0.1);
		_Stick.SetAxisValue(Axis.Z, 0.1);
		runtime.ProcessWithDefaultFrameTime();
		var pitchAtRest = _Output.GetAxisValue(Axis.Y);
		var twistAtRest = _Output.GetAxisValue(Axis.Z);

		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessWithDefaultFrameTime();

		var pitchShift = _Output.GetAxisValue(Axis.Y) - pitchAtRest;
		var twistShift = _Output.GetAxisValue(Axis.Z) - twistAtRest;

		Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);
		Assert.NotEqual(0.0, twistShift);
		// Equal magnitude, opposite direction.
		Assert.Equal(-pitchShift, twistShift, Precision);
	}

	private IFakesOutputRuntimeContext BuildThreeRoutes(BlendedAxisCurve modifier) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteToSameAxisOnOutput(_Output, modifier: modifier),
				_Stick.BindAxis(Axis.Y).RouteToSameAxisOnOutput(_Output, modifier: modifier),
				_Stick.BindAxis(Axis.Z).RouteToSameAxisOnOutput(_Output, modifier: modifier),
			],
		});
}
