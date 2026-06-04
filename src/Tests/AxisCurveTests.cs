namespace SharpSticks.Tests;

/// <summary>
/// Pure-function coverage of <see cref="AxisCurve"/>. Exponent is tested over
/// its supported (0, ∞) range — output = Max · sign(input) · |input|^Exponent;
/// Max in the supported [0, 1] range.
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
	public void Flat_Max0_AlwaysReturnsZero(double input)
	{
		using var runtime = BuildRuntime(new() { Max = 0.0, Exponent = 2.0 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(0.0, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// Exponent > 1 ⇒ ease-out (smaller near zero).
	[InlineData(0.0, 0.0)]
	[InlineData(0.5, 0.25)] // 0.5^2
	[InlineData(1.0, 1.0)]
	[InlineData(-0.5, -0.25)]
	[InlineData(-1.0, -1.0)]
	public void EaseOut_Exponent2_AppliesQuadratic(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Exponent = 2.0 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// Exponent < 1 ⇒ ease-in (larger near zero).
	[InlineData(0.0, 0.0)]
	[InlineData(0.25, 0.5)] // sqrt(0.25)
	[InlineData(1.0, 1.0)]
	[InlineData(-0.25, -0.5)]
	[InlineData(-1.0, -1.0)]
	public void EaseIn_Exponent0_5_AppliesSquareRoot(double input, double expected)
	{
		using var runtime = BuildRuntime(new() { Max = 1.0, Exponent = 0.5 });
		_Stick.SetAxisValue(Axis.X, input);
		runtime.ProcessFrame();
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	// A zero or negative exponent would be a hard step / blow up toward
	// center — not representable; the init must reject it.
	[InlineData(0.0)]
	[InlineData(-0.5)]
	[InlineData(-2.4)]
	public void Exponent_ZeroOrNegative_Throws(double exponent)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new AxisCurve { Exponent = exponent });
	}

	[Fact]
	public void NonLinearMaxScales_OutputByMax()
	{
		// Exponent=2 (quadratic), Max=0.5: input 0.5 → 0.5 * 0.25 = 0.125
		using var runtime = BuildRuntime(new() { Max = 0.5, Exponent = 2.0 });
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

		var flat = new AxisCurve { Max = 0.0 };
		Assert.True(flat.IsLinear);
		Assert.True(flat.IsFlat);

		var nonLinear = new AxisCurve { Exponent = 0.5 };
		Assert.False(nonLinear.IsLinear);
		Assert.False(nonLinear.IsFlat);
	}

	private IFakesOutputRuntimeContext BuildRuntime(AxisCurve curve) =>
		FakesRuntime.Build(new()
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
