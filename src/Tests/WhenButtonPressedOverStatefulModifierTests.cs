namespace SharpSticks.Tests;

/// <summary>
/// Guards <see cref="WhenButtonPressedAxisModifier"/> against the
/// double-<c>Apply</c> hazard: its stateful integration needs the active
/// branch's value at the previous input, and naively re-running the branch's
/// <c>Apply</c> for that probe mutates a *stateful* branch (e.g.
/// <see cref="BlendedAxisCurve"/>) twice per frame, corrupting its latched
/// integrator. The wrapper must instead reuse the branch's actual previous
/// output (same branch) or probe side-effect-free via
/// <see cref="ApplyMode.Peek"/> (branch just switched).
///
/// Spec under test: with the button never pressed, wrapping a stateful
/// modifier in <c>Stateful = Both</c> must be fully transparent — the
/// wrapped route must produce the exact same output as the bare modifier,
/// including the fade toward the latched value when only the blend lever
/// moves. (<c>Stateful = WhenNotPressed</c> takes the same code path while
/// the button is up and is equally covered.)
/// </summary>
public sealed class WhenButtonPressedOverStatefulModifierTests : IDisposable
{
	private const double Precision = 1e-9;
	private const int Button1 = 1;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public WhenButtonPressedOverStatefulModifierTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X)
			.AddAxis(Axis.Slider1)
			.AddButtons(4)
			.Build();

		// X carries the bare reference modifier, Y the wrapped one.
		_Output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.X)
			.AddAxis(Axis.Y)
			.Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void StatefulBoth_OverStatefulBlendedCurve_MatchesBareModifier_WhenButtonNeverPressed()
	{
		// Two independent but identically-configured stateful blends.
		var bare = NewBlend();
		var wrapped = new WhenButtonPressedAxisModifier
		{
			Buttons = [_Stick.BindButton(Button1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = NewBlend(),
			Stateful = WhenButtonPressedStateful.Both,
		};

		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteToSameAxisOnOutput(_Output, modifier: bare),
				_Stick.BindAxis(Axis.X).RouteTo(_Output.BindAxis(Axis.Y), modifier: wrapped),
			],
		});

		// Frame 1: engage halfway (factorT = 0.5) with the input at rest.
		// Both blends seed their latched value from the normal curve: 0.0.
		_Stick.SetAxisValue(Axis.X, 0.0);
		_Stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		AssertBothOutputs(expected: 0.0);

		// Frame 2: move the input 0.0 → 0.8, blend fixed at 0.5.
		// blended(x) = x*0.5 + 0.5x*0.5 = 0.75x
		// latched   = 0.0 + 0.75*0.8 = 0.6
		// output    = lerp(normal(0.8)=0.8, 0.6, factorT=0.5) = 0.7
		// (The wrapper's second Apply already rolls its inner blend's state
		// back to the previous input here, but the corruption is not yet
		// visible in the output.)
		_Stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		AssertBothOutputs(expected: 0.7);

		// Frame 3: input HELD at 0.8, lever pulled fully (factorT 0.5 → 1.0).
		// The input didn't move, so the latched value must stay at 0.6 and
		// the output must fade fully onto it:
		//   output = lerp(normal(0.8)=0.8, 0.6, factorT=1.0) = 0.6
		// The wrapped route instead re-integrates the stale previous-input
		// delta under the NEW blend and its outer integrator sees a zero
		// branch delta — the output freezes at 0.7 and the inner latched
		// value is corrupted to 0.4.
		_Stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		AssertBothOutputs(expected: 0.6);

		// Frame 4: the corruption persists — move the input 0.8 → 0.6 while
		// fully engaged.
		//   latched = 0.6 + (precision(0.6) - precision(0.8)) = 0.6 - 0.1 = 0.5
		//   output  = 0.5
		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		AssertBothOutputs(expected: 0.5);
	}

	private void AssertBothOutputs(double expected)
	{
		// The bare route is the behavioral reference (mirrors
		// BlendedAxisCurveTests) — if this fails, the expectation itself
		// is wrong, not the wrapper.
		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);

		// The wrapper must be transparent while the button is never pressed.
		Assert.Equal(expected, _Output.GetAxisValue(Axis.Y), Precision);
	}

	private BlendedAxisCurve NewBlend() => new()
	{
		NormalCurve = new AxisCurve { Max = 1.0 },
		PrecisionCurve = new AxisCurve { Max = 0.5 },
		ModifierAxes = [_Stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned }],
		Stateful = true,
	};
}
