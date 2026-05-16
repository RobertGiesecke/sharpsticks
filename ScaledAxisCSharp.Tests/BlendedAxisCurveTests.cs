namespace ScaledAxisCSharp.Tests;

/// <summary>
/// Captures the desired behavior of <see cref="BlendedAxisCurve.Stateful"/>:
/// engaging the modifier holds the current output, moving the input while
/// engaged drifts the output through the precision curve only, and slowly
/// releasing the modifier fades the latched offset back toward the normal
/// curve (i.e. toward the rest position of the output axis when the input
/// itself is at rest).
/// </summary>
public sealed class BlendedAxisCurveTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public BlendedAxisCurveTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X)
			.AddAxis(Axis.Slider1)
			.Build();

		_Output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.X)
			.Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Stateful_OutputMatchesNormalCurve_WhenModifierAtRest()
	{
		using var runtime = BuildRuntime(NewModifier());

		_Stick.SetAxisValue(Axis.X, 0.3);
		runtime.ProcessFrame();
		Assert.Equal(0.3, _Output.GetAxisValue(Axis.X), Precision);

		_Stick.SetAxisValue(Axis.X, -0.7);
		runtime.ProcessFrame();
		Assert.Equal(-0.7, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_HoldsValueOnEngage_WhenInputIsSteady()
	{
		using var runtime = BuildRuntime(NewModifier());

		// Modifier at rest, input at 0.8 → identity curve.
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Fully engage the modifier with input unchanged.
		// Output MUST stay at 0.8 — only future input changes should
		// move through the precision curve.
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_IntegratesPrecisionCurveDelta_WhenFullyEngaged()
	{
		using var runtime = BuildRuntime(NewModifier());

		// Seed: input 0.8, fully engaged → latched at 0.8.
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Move input to 0.6 while engaged.
		// delta = precision(0.6) - precision(0.8) = 0.3 - 0.4 = -0.1
		// new output = 0.8 + (-0.1) = 0.7
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// And again: 0.6 → 0.4.
		// delta = precision(0.4) - precision(0.6) = 0.2 - 0.3 = -0.1
		// new output = 0.7 + (-0.1) = 0.6
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_OffsetFadesProportionallyToFactorT_OnSlowRelease()
	{
		// This is the property the button modifier doesn't have: as the
		// modifier axis is gradually released, the latched offset (the
		// gap between output and the normal curve) shrinks proportionally,
		// landing exactly on the normal curve at full rest.
		using var runtime = BuildRuntime(NewModifier());

		// Seed at 0.8, engage, drift to 0.6 → latched _LastOutput = 0.7.
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Now slowly release. Input held steady at 0.6 — only the modifier
		// axis moves, so the latched value doesn't drift; the visible
		// output blends between the normal curve (0.6) and the latched
		// value (0.7) by factorT.
		_Stick.SetAxisValue(Axis.Slider1, 0.75);
		runtime.ProcessFrame();
		Assert.Equal(0.675, _Output.GetAxisValue(Axis.X), Precision);

		_Stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.65, _Output.GetAxisValue(Axis.X), Precision);

		_Stick.SetAxisValue(Axis.Slider1, 0.25);
		runtime.ProcessFrame();
		Assert.Equal(0.625, _Output.GetAxisValue(Axis.X), Precision);

		// Full release → fully on the normal curve.
		_Stick.SetAxisValue(Axis.Slider1, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_ResetsStateOnReturnToRest_AndReSeedsFromCurrentNormal()
	{
		using var runtime = BuildRuntime(NewModifier());

		// Engage at 0.8, drift to 0.6 → latched 0.7.
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Release fully — back to normal curve, state cleared.
		_Stick.SetAxisValue(Axis.Slider1, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);

		// Move input while at rest — pure normal curve.
		_Stick.SetAxisValue(Axis.X, 0.4);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);

		// Re-engage. The previous latched 0.7 must not resurface — the
		// new seed is the current normal-curve output (0.4).
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_ClampsOutputAtAxisLimits_AndBleedsExcessFromLatchedValue()
	{
		// Use an amplifying precision curve (Max=2) so the latched value
		// can be driven past ±1, exercising the back-solve.
		using var runtime = BuildRuntime(NewModifier(normalMax: 1.0, precisionMax: 2.0));

		_Stick.SetAxisValue(Axis.X, 0.5);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);

		// Drift input upward: 0.5 → 0.8.
		// delta = precision(0.8) - precision(0.5) = 1.6 - 1.0 = 0.6
		// latched = 0.5 + 0.6 = 1.1
		// output = lerp(normal(0.8)=0.8, 1.1, factorT=1) = 1.1 → clamped to 1.0
		// _LastOutput back-solved to (1.0 - 0.8*0) / 1 = 1.0.
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(1.0, _Output.GetAxisValue(Axis.X), Precision);

		// Now reverse: 0.8 → 0.5.
		// delta = precision(0.5) - precision(0.8) = 1.0 - 1.6 = -0.6
		// latched = 1.0 + (-0.6) = 0.4
		// output = 0.4. No wind-up: the reversal moves immediately, NOT
		// after un-doing 1.1 - 1.0 = 0.1 of imaginary drift.
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_HandlesNegativeInputs_Symmetrically()
	{
		using var runtime = BuildRuntime(NewModifier());

		_Stick.SetAxisValue(Axis.X, -0.8);
		runtime.ProcessFrame();
		Assert.Equal(-0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Engage with input held → output stays at -0.8.
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(-0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Move input toward zero: -0.8 → -0.6.
		// delta = precision(-0.6) - precision(-0.8) = -0.3 - -0.4 = 0.1
		// latched = -0.8 + 0.1 = -0.7
		_Stick.SetAxisValue(Axis.X, -0.6);
		runtime.ProcessFrame();
		Assert.Equal(-0.7, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_IntegratesCleanlyAcrossZero()
	{
		using var runtime = BuildRuntime(NewModifier());

		// Engage at +0.5 → latched 0.5.
		_Stick.SetAxisValue(Axis.X, 0.5);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);

		// Pull input across zero: +0.5 → -0.5.
		// delta = precision(-0.5) - precision(0.5) = -0.25 - 0.25 = -0.5
		// latched = 0.5 + -0.5 = 0.0
		_Stick.SetAxisValue(Axis.X, -0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);

		// Continue past: -0.5 → -1.0.
		// delta = precision(-1.0) - precision(-0.5) = -0.5 - -0.25 = -0.25
		// latched = 0.0 + -0.25 = -0.25
		_Stick.SetAxisValue(Axis.X, -1.0);
		runtime.ProcessFrame();
		Assert.Equal(-0.25, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_HonorsCustomRestThreshold()
	{
		// Threshold of 0.1: any factorT ≤ 0.1 is considered "at rest" and
		// snaps back to the normal curve, dropping latched state.
		using var runtime = BuildRuntime(NewModifier() with { RestThreshold = 0.1 });

		// Build a latched offset.
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.7, _Output.GetAxisValue(Axis.X), Precision);

		// Above threshold: lerp still active.
		// output = lerp(normal(0.6)=0.6, latched=0.7, factorT=0.2) = 0.62
		_Stick.SetAxisValue(Axis.Slider1, 0.2);
		runtime.ProcessFrame();
		Assert.Equal(0.62, _Output.GetAxisValue(Axis.X), Precision);

		// At-threshold counts as "at rest" (`<=`) → snap to normal.
		_Stick.SetAxisValue(Axis.Slider1, 0.1);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);

		// Below threshold stays at normal.
		_Stick.SetAxisValue(Axis.Slider1, 0.05);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);

		// Re-engage above threshold → fresh seed from current normal (0.6),
		// not the stale 0.7.
		_Stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Stateful_RespectsAsymmetricFactorLowFactorHigh()
	{
		// FactorLow=0.2, FactorHigh=0.8 ⇒ engaged blend lives in [0.2, 0.8],
		// scaled by factorT. The output lerp still uses raw factorT, so
		// modifier-at-rest still pulls output to the normal curve.
		using var runtime = BuildRuntime(
			NewModifier() with { FactorLow = 0.2, FactorHigh = 0.8 });

		// Engage halfway, input at 0.8.
		// First stateful frame: seed latched = normal(0.8) = 0.8.
		// output = lerp(0.8, 0.8, factorT=0.5) = 0.8.
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Drift input to 0.6, factorT still 0.5 → blend = 0.5.
		// blended_at_new  = 0.6*0.5 + 0.3*0.5 = 0.45
		// blended_at_last = 0.8*0.5 + 0.4*0.5 = 0.6
		// delta = -0.15. latched = 0.8 - 0.15 = 0.65.
		// output = lerp(normal(0.6)=0.6, 0.65, 0.5) = 0.625.
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.625, _Output.GetAxisValue(Axis.X), Precision);

		// Fully engage. Input steady, so latched stays at 0.65.
		// output = lerp(0.6, 0.65, factorT=1.0) = 0.65.
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.65, _Output.GetAxisValue(Axis.X), Precision);

		// Release fully → normal curve (state reset), even though
		// FactorLow=0.2 would otherwise leave a non-zero blend.
		_Stick.SetAxisValue(Axis.Slider1, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void NonStateful_AlwaysReturnsBlendedCurveAtCurrentInput()
	{
		// Sanity check that disabling Stateful keeps the legacy lerp-blend
		// behavior: no latching, transitions jump as before.
		using var runtime = BuildRuntime(NewModifier(stateful: false));

		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, _Output.GetAxisValue(Axis.X), Precision);

		// Half-engaged → blend = 0.5, blended(0.8) = 0.8*0.5 + 0.4*0.5 = 0.6.
		_Stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);

		// Fully engaged → blend = 1.0, output = precision(0.8) = 0.4.
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.4, _Output.GetAxisValue(Axis.X), Precision);
	}

	private BlendedAxisCurve NewModifier(
		double normalMax = 1.0,
		double precisionMax = 0.5,
		bool stateful = true) => new()
	{
		NormalCurve = new() { Max = normalMax },
		PrecisionCurve = new() { Max = precisionMax },
		ModifierAxis = _Stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned },
		Stateful = stateful,
	};

	private IOutputRuntimeContext BuildRuntime(BlendedAxisCurve modifier) =>
		Runtime.Build(new()
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
