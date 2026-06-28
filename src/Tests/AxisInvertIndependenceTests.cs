namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// Inverting one route off a source axis must not affect another route off the same
/// axis. Invert lives on the (immutable) <c>AxisBinding</c>, so a <c>.Invert()</c>
/// copy is a distinct binding — the original and any other routes keep their own sign.
/// </summary>
public sealed class AxisInvertIndependenceTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public AxisInvertIndependenceTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Output = _Fakes.AddOutputDevice().AddAxis(Axis.X).AddAxis(Axis.Y).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void SameAxis_NormalAndInverted_ToTwoOutputAxes_StayIndependent()
	{
		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteTo(_Output.BindAxis(Axis.X)),
				_Stick.BindAxis(Axis.X).Invert().RouteTo(_Output.BindAxis(Axis.Y)),
			],
		});

		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();

		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);   // normal route unaffected
		Assert.Equal(-0.6, _Output.GetAxisValue(Axis.Y), Precision);  // inverted route
	}

	[Fact]
	public void SameAxis_NormalAndInverted_ToScroll_StayIndependent()
	{
		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			InputSynthesizer = _Synth,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteToScroll(ScrollAxis.Vertical, sensitivity: 10),
				_Stick.BindAxis(Axis.X).Invert().RouteToScroll(ScrollAxis.Horizontal, sensitivity: 10),
			],
		});

		_Stick.SetAxisValue(Axis.X, 1.0);
		_Time.Advance(TimeSpan.FromSeconds(1));
		runtime.ProcessFrame();                       // baseline frame (elapsed 0)
		_Time.Advance(TimeSpan.FromSeconds(1));
		runtime.ProcessFrame();                       // 1 s × 1.0 × 10 = 10 notches

		var scroll = Assert.Single(_Synth.Events);
		Assert.Equal(EventKind.Scroll, scroll.Kind);
		Assert.Equal(10, scroll.Dy);    // vertical, normal → +10
		Assert.Equal(-10, scroll.Dx);   // horizontal, inverted → -10
	}
}
