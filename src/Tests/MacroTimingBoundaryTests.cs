namespace SharpSticks.Tests;

/// <summary>
/// Pins the macro engine's wall-clock semantics at their boundaries: a
/// <c>WaitFor</c> deadline fires exactly when <c>now &gt;= deadline</c> (never
/// a tick early), <c>WaitFor(0)</c> still defers the rest of the macro to the
/// next frame, and a huge frame gap completes at most one wait segment per
/// frame — each wait is anchored to the frame that reaches it, deadlines do
/// not chain off each other.
/// </summary>
public sealed class MacroTimingBoundaryTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public MacroTimingBoundaryTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Output = _Fakes.AddOutputDevice().AddButtons(8).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void WaitFor_HoldsOneTickBeforeTheDeadline_AndFiresExactlyOnIt()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(target),
				Macros.WaitFor(50.Milliseconds),
				Macros.Release(target),
			],
		});

		runtime.ProcessWithDefaultFrameTime();
		_Stick.PressButton(1);
		// The wait's deadline is anchored to this frame's instant.
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(3));

		// One tick short of the deadline: must still hold.
		runtime.ProcessFrame(50.Milliseconds - FromTicks(1));
		Assert.True(_Output.GetButtonState(3));

		// Exactly on the deadline: fires.
		runtime.ProcessFrame(FromTicks(1));
		Assert.False(_Output.GetButtonState(3));
	}

	[Fact]
	public void WaitForZero_StillDefersTheRest_ToTheNextFrame()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(target),
				Macros.WaitFor(TimeSpan.Zero),
				Macros.Release(target),
			],
		});

		runtime.ProcessWithDefaultFrameTime();
		_Stick.PressButton(1);
		// A zero wait is a one-frame yield: Press runs, Release does not.
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(3));

		// Even a zero-elapsed frame satisfies the deadline (now >= deadline).
		runtime.ProcessFrame(TimeSpan.Zero);
		Assert.False(_Output.GetButtonState(3));
	}

	[Fact]
	public void HugeFrameGap_CompletesOnlyOneWaitSegmentPerFrame()
	{
		var btnA = _Output.BindButton(3);
		var btnB = _Output.BindButton(4);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(btnA),
				Macros.WaitFor(10.Milliseconds),
				Macros.Press(btnB),
				Macros.WaitFor(10.Milliseconds),
				Macros.Release(btnA),
				Macros.Release(btnB),
			],
		});

		runtime.ProcessWithDefaultFrameTime();
		_Stick.PressButton(1);
		runtime.ProcessWithDefaultFrameTime();
		Assert.True(_Output.GetButtonState(3));
		Assert.False(_Output.GetButtonState(4));

		// A frame far past BOTH deadlines: the first wait completes and
		// Press(B) runs, but the second wait is anchored to THIS frame — the
		// releases stay pending no matter how large the gap was.
		runtime.ProcessFrame(10.Seconds);
		Assert.True(_Output.GetButtonState(3));
		Assert.True(_Output.GetButtonState(4));

		// The second wait completes one frame later.
		runtime.ProcessFrame(10.Milliseconds);
		Assert.False(_Output.GetButtonState(3));
		Assert.False(_Output.GetButtonState(4));
	}

	private IFakesOutputRuntimeContext Build(params IConfigurableRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});
}
