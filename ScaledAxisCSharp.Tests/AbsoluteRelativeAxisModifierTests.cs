namespace ScaledAxisCSharp.Tests;

/// <summary>
/// Behavioral coverage for the absolute-axis-via-two-relatives feature
/// (`BindingExtensions.RouteAbsoluteRelative`). Each test drives the runtime
/// frame-by-frame with `ProcessFrame` and asserts on the two output axes that
/// the routes produce. `IncreaseRestPosition=DecreaseRestPosition=0.5` is used
/// in most tests so `MapPulseToSignedOutput(restPos, pulse)` simplifies to
/// `output == pulseMagnitude` for clean assertions.
/// </summary>
public sealed class AbsoluteRelativeAxisModifierTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
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

	[Fact]
	public void AtRest_BothOutputsHoldRestPosition_WhenInputMatchesInitialValue()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.5));

		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void PositiveError_PulsesIncreaseAxis_DecreaseHoldsRest()
	{
		using var runtime = BuildRuntime(
			MakeOptions(initial: 0.0) with { OutputRiseRate = 1.0, Gain = 1.0 });

		// target=1, error=1 → desired pulse = error*Gain = 1, capped at MaxOutput=1.
		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void NegativeError_PulsesDecreaseAxis_IncreaseHoldsRest()
	{
		using var runtime = BuildRuntime(
			MakeOptions(initial: 1.0) with { OutputRiseRate = 1.0, OutputFallRate = 1.0, Gain = 1.0 });

		// target=0, error=-1 → Decrease pulses at magnitude 1.
		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void Converges_ToTargetOverMultipleFrames_WhenInputStaysFixed()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.25,        // Current advances by pulse * rate per frame
			Gain = 10.0,                // saturate pulse to MaxOutput while error > 0.1
			ErrorTolerance = 1e-6,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);  // target=1

		// While |error|*Gain >= MaxOutput, pulse stays at 1.0, step = 1*0.25 = 0.25.
		// Current: 0 → 0.25 → 0.5 → 0.75 → 1.0 across four frames.
		for (var i = 0; i < 4; i++)
		{
			runtime.ProcessFrame();
			Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		}

		// Frame 5: Current at target → pulse drops to 0, output slews to rest immediately.
		runtime.ProcessFrame();
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
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.0,  // freeze Current so we can inspect pulse alone
			Gain = 1.0,
		});

		// Source 0.5 → normalized 0.5 → target 0.5. error=0.5. pulse=0.5.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.8 → top of source range → target=1. error=1. pulse=1.
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.95 (above source max) → clamped to source max → target=1. pulse=1.
		_Stick.SetAxisValue(Axis.X, 0.95);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Source 0.1 (below source min) → clamped → target=0. Current still 0 → error=0.
		_Stick.SetAxisValue(Axis.X, 0.1);
		runtime.ProcessFrame();
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
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.0,  // freeze Current
			Gain = gain,
		});

		_Stick.SetAxisValue(Axis.X, inputValue);
		runtime.ProcessFrame();
		Assert.Equal(expectedPulse, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void MaxOutput_CapsTheDesiredPulseMagnitude()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 10.0,
			MaxOutput = 0.3,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void MinOutput_FloorsTheDesiredPulse_OnceNonZero()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 0.1,
			MinOutput = 0.2,
		});

		// Base pulse = 0.5 * 0.1 = 0.05; floored up to MinOutput=0.2.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.2, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void ErrorTolerance_StopsPulsing_WhenWithinToleranceOfTarget()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.5) with
		{
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			Gain = 10.0,
			ErrorTolerance = 0.05,
		});

		// Input 0.52 → target=0.52, |error|=0.02 ≤ 0.05 → pulse=0.
		_Stick.SetAxisValue(Axis.X, 0.52);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider2), Precision);
	}

	[Fact]
	public void OutputRiseRate_LimitsPulseRiseSpeed_PerFrame()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 0.2,
			OutputFallRate = 0.2,
			IncreaseRate = 0.0,  // freeze Current so desired stays saturated
			Gain = 10.0,
			Maximum = 10.0,      // keep error large
		});

		_Stick.SetAxisValue(Axis.X, 1.0);

		// Pulse magnitude ramps by 0.2 per frame toward saturated desired=1.0.
		runtime.ProcessFrame();
		Assert.Equal(0.2, _Output.GetAxisValue(Axis.Slider1), Precision);

		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.Slider1), Precision);

		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void OutputFallRate_LimitsPulseFallSpeed_WhenDesiredDropsToZero()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 1.0,
			OutputFallRate = 0.25,
			IncreaseRate = 0.0,
			Gain = 1.0,
		});

		// Frame 1: input=1 → desired=1 → pulse rises to 1 immediately.
		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Frame 2: input back to 0 → target=0 → error sign flips, Increase desired=0.
		// Slew 1 → 0 by 0.25 per frame.
		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(0.75, _Output.GetAxisValue(Axis.Slider1), Precision);

		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void IncreaseEdgeBoost_AmplifiesPulse_AsTargetApproachesMax()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 0.0) with
		{
			OutputRiseRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 0.1,
			IncreaseEdgeBoost = 2.0,
		});

		// Target at midpoint: edgeBias = 1 + 2 * 0.5 = 2. Base = 0.5*0.1 = 0.05. Out = 0.1.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.1, _Output.GetAxisValue(Axis.Slider1), Precision);

		// Target at max: edgeBias = 1 + 2 * 1 = 3. Base = 1*0.1 = 0.1. Out = 0.3.
		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.Slider1), Precision);
	}

	[Fact]
	public void DecreaseEdgeBoost_AmplifiesPulse_AsTargetApproachesMin()
	{
		using var runtime = BuildRuntime(MakeOptions(initial: 1.0) with
		{
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.0,
			DecreaseRate = 0.0,  // freeze Current on the Decrease side too
			Gain = 0.1,
			DecreaseEdgeBoost = 2.0,
		});

		// Target at midpoint: edgeBias = 1 + 2 * (1 - 0.5) = 2. Base = 0.5*0.1 = 0.05. Out = 0.1.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.1, _Output.GetAxisValue(Axis.Slider2), Precision);

		// Target at min: edgeBias = 1 + 2 * 1 = 3. Base = 1*0.1 = 0.1. Out = 0.3.
		_Stick.SetAxisValue(Axis.X, 0.0);
		runtime.ProcessFrame();
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
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 1.0,
		});

		// At rest: pulse=0 → output = -1 + 0*2 = -1 on both axes.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.Slider2), Precision);

		// Half pulse: target=1, error=0.5, pulse=0.5. Out = -1 + 0.5*2 = 0.
		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
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
			OutputRiseRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 1.0,
		});

		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
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
			OutputRiseRate = 1.0,
			OutputFallRate = 1.0,
			IncreaseRate = 0.0,
			Gain = 1.0,
		});

		// InitialValue 5 clamped to 1. Input 1 → target 1 → error 0 → pulse 0.
		_Stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.Slider1), Precision);
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

	private IOutputRuntimeContext BuildRuntime(AbsoluteRelativeAxisOptions options)
	{
		var routes = _Stick.BindAxis(Axis.X).RouteAbsoluteRelative(options);
		return Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});
	}
}
