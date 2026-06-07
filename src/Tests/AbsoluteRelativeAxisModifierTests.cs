namespace SharpSticks.Tests;

/// <summary>
/// Behavioral coverage for the absolute-axis-via-two-relatives feature
/// (`BindingExtensions.RouteAbsoluteRelative`). Each test drives the runtime
/// frame-by-frame with <see cref="Step"/>, which advances virtual time by
/// exactly one second per frame. The model rate is wall-clock based
/// (<c>step = pulse · (range / SecondsToFull) · elapsedSeconds</c>), so with a
/// 1 s frame and range 1 a <c>SecondsToFull</c> of <c>N</c> advances the model
/// by <c>pulse / N</c> per frame — e.g. <c>SecondsToFull = 4</c> ⇒ 0.25/frame.
/// <see cref="double.PositiveInfinity"/> freezes the model (used to isolate the
/// output pulse). <c>IncreaseRestPosition=DecreaseRestPosition=0.5</c> in most
/// tests so the signed-output mapping simplifies to <c>output == pulse</c>.
/// </summary>
public sealed class AbsoluteRelativeAxisModifierTests : IDisposable
{
	private const double Precision = 1e-9;
	private static readonly TimeSpan FrameDt = TimeSpan.FromSeconds(1);

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeTimeSource _Time = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public AbsoluteRelativeAxisModifierTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.Slider1)  // IncreaseAxis
			.AddAxis(Axis.Slider2)  // DecreaseAxis
			.Build();
	}

	public void Dispose() => _Fakes.Dispose();

	// One frame = advance virtual time by 1 s, then process. The wall-clock
	// model rate then sees a fixed 1 s elapsed every frame.
	private void Step(IFakesOutputRuntimeContext runtime)
	{
		_Time.Advance(FrameDt);
		runtime.ProcessFrame();
	}

	[Fact]
	public void AtRest_BothOutputsHoldRestPosition_WhenInputMatchesInitialValue()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.5));

		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void PositiveError_PulsesIncreaseAxis_DecreaseHoldsRest()
	{
		using var runtime = BuildRuntime(
			MakeOptions(initial: 0.0) with { OutputRiseSeconds = 1.0, Gain = 1.0 });

		// target=1, error=1 → desired pulse = error*Gain = 1, capped at MaxOutput=1.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void NegativeError_PulsesDecreaseAxis_IncreaseHoldsRest()
	{
		using var runtime = BuildRuntime(
			MakeOptions(initial: 1.0) with { OutputRiseSeconds = 1.0, OutputFallSeconds = 1.0, Gain = 1.0 });

		// target=0, error=-1 → Decrease pulses at magnitude 1.
		_Stick.SetAxisValue(Axis.X, 0.0);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void Converges_ToTargetOverMultipleFrames_WhenInputStaysFixed()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = 4.0,  // full range in 4 s → 0.25/frame at 1 s/frame
			Gain = 10.0,                  // saturate pulse to MaxOutput while error > 0.1
			ErrorTolerance = 1e-6,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);  // target=1

		// While |error|*Gain >= MaxOutput, pulse stays at 1.0, step = 1*0.25 = 0.25.
		// Current: 0 → 0.25 → 0.5 → 0.75 → 1.0 across four frames.
		for (var i = 0; i < 4; i++)
		{
			Step(runtime);
			Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		}

		// Frame 5: Current at target → pulse drops to 0, output slews to rest immediately.
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void SourceInputRange_MapsLinearlyOntoTargetRange_AndClampsOutside()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			SourceInputMinimum = 0.2,
			SourceInputMaximum = 0.8,
			Minimum = 0.0,
			Maximum = 1.0,
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,  // freeze Current so we inspect pulse alone
			Gain = 1.0,
		});

		// Source 0.5 → normalized 0.5 → target 0.5. error=0.5. pulse=0.5.
		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.8 → top of source range → target=1. error=1. pulse=1.
		_Stick.SetAxisValue(Axis.X, 0.8);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.95 (above source max) → clamped to source max → target=1. pulse=1.
		_Stick.SetAxisValue(Axis.X, 0.95);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.1 (below source min) → clamped → target=0. Current still 0 → error=0.
		_Stick.SetAxisValue(Axis.X, 0.1);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Theory]
	[InlineData(1.0, 0.3, 0.3)] // 0.3 * 1 = 0.3
	[InlineData(2.0, 0.3, 0.6)] // 0.3 * 2 = 0.6
	[InlineData(4.0, 0.3, 1.0)] // 0.3 * 4 = 1.2, capped at MaxOutput=1
	public void Gain_ScalesDesiredPulseByErrorMagnitude(double gain, double inputValue, double expectedPulse)
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,  // freeze Current
			Gain = gain,
		});

		_Stick.SetAxisValue(Axis.X, inputValue);
		Step(runtime);
		Assert.Equal(expectedPulse, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void MaxOutput_CapsTheDesiredPulseMagnitude()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 10.0,
			MaxOutput = 0.3,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void MinOutput_FloorsTheDesiredPulse_OnceNonZero()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 0.1,
			MinOutput = 0.2,
		});

		// Base pulse = 0.5 * 0.1 = 0.05; floored up to MinOutput=0.2.
		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.2, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void ErrorTolerance_StopsPulsing_WhenWithinToleranceOfTarget()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.5) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			Gain = 10.0,
			ErrorTolerance = 0.05,
		});

		// Input 0.52 → target=0.52, |error|=0.02 ≤ 0.05 → pulse=0.
		_Stick.SetAxisValue(Axis.X, 0.52);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void OutputRiseSeconds_LimitsPulseRiseSpeed()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 5.0,  // 0→1 over 5 s → 0.2/frame at 1 s/frame
			OutputFallSeconds = 5.0,
			IncreaseSecondsToFull = double.PositiveInfinity,  // freeze Current so desired stays saturated
			Gain = 10.0,
			Maximum = 10.0,      // keep error large
		});

		_Stick.SetAxisValue(Axis.X, 1.0);

		// Pulse magnitude ramps by 0.2 per frame toward saturated desired=1.0.
		Step(runtime);
		Assert.Equal(0.2, _Output.GetAxisValue(Axis.Slider1), Precision);

		Step(runtime);
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.Slider1), Precision);

		Step(runtime);
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void OutputFallSeconds_LimitsPulseFallSpeed_WhenDesiredDropsToZero()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 4.0,  // 1→0 over 4 s → 0.25/frame at 1 s/frame
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 1.0,
		});

		// Frame 1: input=1 → desired=1 → pulse rises to 1 immediately.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Frame 2: input back to 0 → target=0 → error sign flips, Increase desired=0.
		// Slew 1 → 0 by 0.25 per frame.
		_Stick.SetAxisValue(Axis.X, 0.0);
		Step(runtime);
		Assert.Equal(0.75, _Output.GetAxisValue(Axis.Slider1), Precision);

		Step(runtime);
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void IncreaseEdgeBoost_AmplifiesPulse_AsTargetApproachesMax()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 0.1,
			IncreaseEdgeBoost = 2.0,
		});

		// Target at midpoint: edgeBias = 1 + 2 * 0.5 = 2. Base = 0.5*0.1 = 0.05. Out = 0.1.
		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.1, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Target at max: edgeBias = 1 + 2 * 1 = 3. Base = 1*0.1 = 0.1. Out = 0.3.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void DecreaseEdgeBoost_AmplifiesPulse_AsTargetApproachesMin()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 1.0) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			DecreaseSecondsToFull = double.PositiveInfinity,  // freeze Current on the Decrease side too
			Gain = 0.1,
			DecreaseEdgeBoost = 2.0,
		});

		// Target at midpoint: edgeBias = 1 + 2 * (1 - 0.5) = 2. Base = 0.5*0.1 = 0.05. Out = 0.1.
		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.1, _Output.GetAxisValue(Axis.Slider2), Precision);

		// Target at min: edgeBias = 1 + 2 * 1 = 3. Base = 1*0.1 = 0.1. Out = 0.3.
		_Stick.SetAxisValue(Axis.X, 0.0);
		Step(runtime);
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void RestPosition_AffectsSignedOutputMapping()
	{
		// restPosition=0 ⇒ restSigned=-1, full pulse maps to +1.
		using var runtime = BuildRuntime(MakeOptions(initial: 0.5) with
		{
			IncreaseRestPosition = 0.0,
			DecreaseRestPosition = 0.0,
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 1.0,
		});

		// At rest: pulse=0 → output = -1 + 0*2 = -1 on both axes.
		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider2), Precision);

		// Half pulse: target=1, error=0.5, pulse=0.5. Out = -1 + 0.5*2 = 0.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Decrease side at rest still -1 (no pulse).
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void Minimum_Maximum_ReorderIfReversed()
	{
		// Pass Minimum=1, Maximum=0 — implementation should swap them.
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			Minimum = 1.0,
			Maximum = 0.0,
			OutputRiseSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 1.0,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		// Sorted: Min=0, Max=1. target=1, error=1, pulse=1.
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void InitialValue_IsClampedToTargetRange()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 5.0) with
		{
			Minimum = 0.0,
			Maximum = 1.0,
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = double.PositiveInfinity,
			Gain = 1.0,
		});

		// InitialValue 5 clamped to 1. Input 1 → target 1 → error 0 → pulse 0.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	// --- Same axis for Increase and Decrease: bidirectional mode. ---
	// One route on the shared axis; rest is center (0), increase pulses push
	// positive, decrease pulses negative.

	[Fact]
	public void SameAxis_AtRest_OutputsCenter()
	{
		using var runtime = BuildRuntime(MakeSameAxisOptions(initial: 0.5));

		_Stick.SetAxisValue(Axis.X, 0.5);
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void SameAxis_PositiveError_PulsesPositive()
	{
		using var runtime = BuildRuntime(
			MakeSameAxisOptions(initial: 0.0) with { OutputRiseSeconds = 1.0, Gain = 1.0 });

		// target=1, error=+1 → increase pulse 1 → output +1.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void SameAxis_NegativeError_PulsesNegative()
	{
		using var runtime = BuildRuntime(
			MakeSameAxisOptions(initial: 1.0) with { OutputRiseSeconds = 1.0, OutputFallSeconds = 1.0, Gain = 1.0 });

		// target=0, error=-1 → decrease pulse 1 → output -1.
		_Stick.SetAxisValue(Axis.X, 0.0);
		Step(runtime);
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void SameAxis_ConvergesEachWay_AndReturnsToCenter()
	{
		using var runtime = BuildRuntime(MakeSameAxisOptions(initial: 0.0) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 1.0,
			IncreaseSecondsToFull = 4.0,  // 0.25/frame at 1 s/frame
			DecreaseSecondsToFull = 4.0,
			Gain = 10.0,
			ErrorTolerance = 1e-6,
		});

		// Up: while error > 0, output pulses +1; Current 0 → 1 in 0.25 steps.
		_Stick.SetAxisValue(Axis.X, 1.0);
		for (var i = 0; i < 4; i++)
		{
			Step(runtime);
			Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		}

		// Converged → back to center.
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Down: error flips negative, output pulses -1; Current 1 → 0.
		_Stick.SetAxisValue(Axis.X, 0.0);
		for (var i = 0; i < 4; i++)
		{
			Step(runtime);
			Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		}

		// Converged again → center.
		Step(runtime);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void SameAxis_DirectionFlip_OutputIsNetOfDecayingOppositePulse()
	{
		// Freeze Current (infinite seconds-to-full) to isolate the pulse
		// dynamics. Slow fall rate so the increase pulse is still decaying when
		// the decrease pulse rises — the output must be the net of the two.
		using var runtime = BuildRuntime(MakeSameAxisOptions(initial: 0.5) with
		{
			OutputRiseSeconds = 1.0,
			OutputFallSeconds = 2.5,  // 1→0 over 2.5 s → 0.4/frame at 1 s/frame
			IncreaseSecondsToFull = double.PositiveInfinity,
			DecreaseSecondsToFull = double.PositiveInfinity,
			Gain = 10.0,
		});

		// error=+0.5 → increase pulse saturates at 1.
		_Stick.SetAxisValue(Axis.X, 1.0);
		Step(runtime);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Flip: error=-0.5 → decrease rises to 1 immediately, increase decays
		// by 0.4 per frame. Net: 0.6-1, 0.2-1, 0-1.
		_Stick.SetAxisValue(Axis.X, 0.0);
		Step(runtime);
		Assert.Equal(-0.4, _Output.GetAxisValue(Axis.Slider1), Precision);

		Step(runtime);
		Assert.Equal(-0.8, _Output.GetAxisValue(Axis.Slider1), Precision);

		Step(runtime);
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	private AbsoluteRelativeAxisOptions MakeOptions(double initial) => new()
	{
		IncreaseAxis = _Output.BindAxis(Axis.Slider1),
		DecreaseAxis = _Output.BindAxis(Axis.Slider2),
		InitialValue = initial,
		Minimum = 0.0,
		Maximum = 1.0,
		SourceInputMinimum = 0.0,
		SourceInputMaximum = 1.0,
		IncreaseRestPosition = 0.5,
		DecreaseRestPosition = 0.5,
	};

	private AbsoluteRelativeAxisOptions MakeSameAxisOptions(double initial) =>
		MakeOptions(initial) with
		{
			IncreaseAxis = _Output.BindAxis(Axis.Slider1),
			DecreaseAxis = _Output.BindAxis(Axis.Slider1),
		};

	private IFakesOutputRuntimeContext BuildRuntime(AbsoluteRelativeAxisOptions options)
	{
		var routes = _Stick.BindAxis(Axis.X).RouteAbsoluteRelative(options);
		return FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			Routes = [routes],
		});
	}
}
