namespace SharpSticks.Tests;

/// <summary>
/// Pure-function coverage of <see cref="AxisCurve"/>. Steepness is tested in
/// the supported [0, 2] range; Max in the supported [0, 1] range.
/// </summary>
public sealed class AxisCurveTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public AxisCurveTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Theory]
	[InlineData(0.0, 0.0)]
	[InlineData(0.25, 0.25)]
	[InlineData(0.5, 0.5)]
	[InlineData(1.0, 1.0)]
	[InlineData(-0.25, -0.25)]
	[InlineData(-1.0, -1.0)]
	public void Linear_Max1_IsIdentity(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	[InlineData(0.0, 0.5, 0.0)]
	[InlineData(0.5, 0.5, 0.25)]
	[InlineData(1.0, 0.5, 0.5)]
	[InlineData(-1.0, 0.5, -0.5)]
	[InlineData(1.0, 0.0, 0.0)]
	[InlineData(1.0, 0.184, 0.184)]
	public void Linear_ScalesByMax(double input, double max, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = max });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.3)]
	[InlineData(0.9)]
	[InlineData(-0.6)]
	[InlineData(1.0)]
	[InlineData(-1.0)]
	public void Flat_AlwaysReturnsZero(double input)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Steepness = 0.0 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// Steepness < 1: exponent = 1 / steepness > 1 ⇒ ease-out (smaller near zero).
	[InlineData(0.0, 0.0)]
	[InlineData(0.5, 0.25)] // 0.5^2
	[InlineData(1.0, 1.0)]
	[InlineData(-0.5, -0.25)]
	[InlineData(-1.0, -1.0)]
	public void EaseOut_Steepness0_5_ApplesQuadratic(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Steepness = 0.5 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// Steepness > 1: exponent = 2 - steepness < 1 ⇒ ease-in (larger near zero).
	[InlineData(0.0, 0.0)]
	[InlineData(0.25, 0.5)] // sqrt(0.25)
	[InlineData(1.0, 1.0)]
	[InlineData(-0.25, -0.5)]
	[InlineData(-1.0, -1.0)]
	public void EaseIn_Steepness1_5_AppliesSquareRoot(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Steepness = 1.5 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// Steepness = 2 ⇒ exponent = 0 ⇒ output = Max·sign(input) for non-zero input.
	[InlineData(1e-6, 1.0)]
	[InlineData(0.3, 1.0)]
	[InlineData(1.0, 1.0)]
	[InlineData(-1e-6, -1.0)]
	[InlineData(-0.7, -1.0)]
	public void Steepness2_IsAHardStepAcrossZero(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Steepness = 2.0 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void NonLinearMaxScales_OutputByMax()
	{
		// Steepness=0.5 (quadratic), Max=0.5: input 0.5 → 0.5 * 0.25 = 0.125
		using var runtime = BuildRuntime(new() { Max = 0.5, Steepness = 0.5 });
		_Stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.125, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Linear_IsLinearAndIsFlatFlagsSetCorrectly()
	{
		var defaultCurve = new AxisCurve();
		Assert.True(defaultCurve.IsLinear);
		Assert.False(defaultCurve.IsFlat);

		var flat = new AxisCurve { Steepness = 0.0 };
		Assert.False(flat.IsLinear);
		Assert.True(flat.IsFlat);

		var nonLinear = new AxisCurve { Steepness = 1.5 };
		Assert.False(nonLinear.IsLinear);
		Assert.False(nonLinear.IsFlat);
	}

	private IOutputRuntimeContext BuildRuntime(AxisCurve curve) =>
		Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteToSameAxisOnOutput(_Output, modifier: curve),
			],
		});
}
