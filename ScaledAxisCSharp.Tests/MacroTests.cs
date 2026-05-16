namespace ScaledAxisCSharp.Tests;

/// <summary>
/// v1 macro coverage: edge-triggered OnPress / OnRelease running through the
/// macro engine, with Press / Release / Wait actions only. Time is driven via
/// <see cref="FakeTimeSource"/> so deferred actions are testable without
/// sleeping. The engine ORs macro-held buttons into the normal button route
/// output, so the parallel-route test verifies both signals reach the output.
/// </summary>
public sealed class MacroTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;
	private readonly FakeTimeSource _Time = new();

	public MacroTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Output = _Fakes.AddOutputDevice().AddButtons(8).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	// ── Press / Release through OnPress / OnRelease ─────────────────────

	[Fact]
	public void OnPress_Press_HoldsTarget_AcrossSubsequentFrames()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [Macros.Press(_Output.BindButton(3))],
		});

		// Warm-up: edge detector establishes "released" baseline.
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		// Rising edge -> OnPress runs; macro completes leaving press in place.
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Subsequent frames keep the press: macro completion does NOT auto-release.
		runtime.ProcessFrame();
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));
	}

	[Fact]
	public void OnRelease_Release_DropsTarget_OnSourceRelease()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [Macros.Press(target)],
			OnRelease = [Macros.Release(target)],
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── Wait defers a subsequent action across frames ───────────────────

	[Fact]
	public void Wait_DefersFollowingActions_UntilTimeAdvancesPastDeadline()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(target),
				Macros.Wait(TimeSpan.FromMilliseconds(50)),
				Macros.Release(target),
			],
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);

		// First post-press frame: Press fires, Wait schedules, output held.
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Time has not advanced enough — Release does NOT run yet.
		_Time.Advance(TimeSpan.FromMilliseconds(20));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		_Time.Advance(TimeSpan.FromMilliseconds(20));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Crossing the 50ms deadline — Release fires this frame.
		_Time.Advance(TimeSpan.FromMilliseconds(20));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── Re-trigger while busy queues a second run (QueueUntilDone) ──────

	[Fact]
	public void DefaultReentry_QueuesAnotherRun_WhileFirstIsStillWaiting()
	{
		var btnA = _Output.BindButton(3);
		var btnB = _Output.BindButton(4);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(btnA),
				Macros.Wait(TimeSpan.FromMilliseconds(50)),
				Macros.Release(btnA),
				Macros.Press(btnB),
			],
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));
		Assert.False(_Output.GetButtonState(4));

		// Release + re-press the source while the first run is still in Wait.
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		// First run is still mid-wait. Second run is queued — has not started.
		Assert.True(_Output.GetButtonState(3));
		Assert.False(_Output.GetButtonState(4));

		// Cross first run's deadline: it finishes (Release A, Press B), then
		// the queued second run starts (Press A, Wait, ...).
		_Time.Advance(TimeSpan.FromMilliseconds(60));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3)); // second run pressed A again
		Assert.True(_Output.GetButtonState(4)); // first run pressed B at the end

		// Cross second run's deadline.
		_Time.Advance(TimeSpan.FromMilliseconds(60));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3)); // second run released A
		Assert.True(_Output.GetButtonState(4)); // B still held (never released)
	}

	// ── OR with a parallel ButtonRoute targeting the same output ────────

	[Fact]
	public void MacroHeldButton_OrsWith_ParallelButtonRoute()
	{
		// stick.B1 routes to output.B3 the classic way.
		// stick.B2 fires a macro that presses output.B3.
		var target = _Output.BindButton(3);
		using var runtime = Build(
			new ButtonRoute(_Stick.BindButton(1), target),
			new ButtonMacroRoute
			{
				Binding = _Stick.BindButton(2),
				OnPress = [Macros.Press(target)],
				OnRelease = [Macros.Release(target)],
			});

		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		// Route presses output.
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Macro asserts too -> still held (OR).
		_Stick.PressButton(2);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Route releases; macro still holding -> still held.
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Macro releases -> output drops.
		_Stick.ReleaseButton(2);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── DropIfBusy ignores re-triggers ──────────────────────────────────

	[Fact]
	public void DropIfBusy_IgnoresReTrigger_WhileMacroIsRunning()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(target),
				Macros.Wait(TimeSpan.FromMilliseconds(50)),
				Macros.Release(target),
			],
			Reentry = MacroReentry.DropIfBusy,
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Re-trigger while busy: dropped, no new queue entry.
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// First run completes normally.
		_Time.Advance(TimeSpan.FromMilliseconds(60));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		// Source still pressed but no edge happens this frame — no new macro starts.
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── CancelAndRestart releases held buttons before restarting ────────

	[Fact]
	public void CancelAndRestart_DropsRunningMacrosHeldButtons_BeforeNewRunStarts()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.Press(target),
				Macros.Wait(TimeSpan.FromMilliseconds(50)),
				Macros.Release(target),
			],
			Reentry = MacroReentry.CancelAndRestart,
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Re-trigger while first is in Wait. First gets cancelled — its press is
		// released — and the new run starts immediately (re-presses).
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		// After cancel: previous press released. New run's Press fires this frame.
		Assert.True(_Output.GetButtonState(3));

		// New run is mid-wait — its press persists.
		_Time.Advance(TimeSpan.FromMilliseconds(20));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// New run completes.
		_Time.Advance(TimeSpan.FromMilliseconds(40));
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── Empty action arrays are no-ops ──────────────────────────────────

	[Fact]
	public void EmptyOnPress_IsNoOp_EvenOnRisingEdge()
	{
		var target = _Output.BindButton(3);
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [],
			OnRelease = [Macros.Release(target)],
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		// OnPress is empty -> nothing happens.
		Assert.False(_Output.GetButtonState(3));
	}

	// ── Force-release: macro overrides a route's assertion ──────────────

	[Fact]
	public void MacroRelease_OverridesRouteAssertion_UntilRouteFallsAndRises()
	{
		// Stick.B1 routes B1 → output.B3 directly. Stick.B2 fires a macro that
		// force-releases B3 while held, then re-presses it after a short wait.
		// While the macro suppresses, output.B3 must follow the macro, not the
		// route. When the user releases-and-re-presses Stick.B1, the macro's
		// suppression clears on the route's falling edge so the next route press
		// asserts the button again.
		var target = _Output.BindButton(3);
		using var runtime = Build(
			new ButtonRoute(_Stick.BindButton(1), target),
			new ButtonMacroRoute
			{
				Binding = _Stick.BindButton(2),
				OnPress =
				[
					Macros.Release(target),
					Macros.Wait(TimeSpan.FromMilliseconds(20)),
					Macros.Press(target),
				],
			});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));   // route holds B3.

		_Stick.PressButton(2);
		runtime.ProcessFrame();
		// Macro asserts force-release on B3. Route still has B1 down, but the
		// suppressor overrides it.
		Assert.False(_Output.GetButtonState(3));

		// Wait elapses. Macro Press re-asserts; suppression clears.
		_Time.Advance(TimeSpan.FromMilliseconds(25));
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));
	}

	[Fact]
	public void RouteFallingEdge_ClearsMacroSuppression_SoNextRoutePressAsserts()
	{
		// Macro force-releases B3 without re-pressing. Route was pressing B3 via
		// B1; macro suppression hides it. If the user releases B1 and presses it
		// again, the suppression should clear on the falling edge so the next
		// route press takes effect.
		var target = _Output.BindButton(3);
		using var runtime = Build(
			new ButtonRoute(_Stick.BindButton(1), target),
			new ButtonMacroRoute
			{
				Binding = _Stick.BindButton(2),
				OnPress = [Macros.Release(target)],
			});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Macro asserts force-release. Suppression engaged.
		_Stick.PressButton(2);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		// Route falling edge clears suppression. Output drops fully (no asserter).
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		// Next route press asserts again — the prior suppression is gone.
		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));
	}

	[Fact]
	public void TwoRouteSourcesOnSameOutput_BothMustReleaseBeforeOutputDrops()
	{
		// Refcount semantics: two ButtonRoutes target the same output. Either
		// one being pressed is enough to hold the output; both must release for
		// the output to drop.
		var target = _Output.BindButton(3);
		using var runtime = Build(
			new ButtonRoute(_Stick.BindButton(1), target),
			new ButtonRoute(_Stick.BindButton(2), target));

		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));

		_Stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		_Stick.PressButton(2);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		// Release one source: output stays held — the route group's edge-tracked
		// contribution stays asserted because the OR over its bindings still
		// asserts. (Per-group contributes 1; the second binding's press is what
		// keeps the group asserting.)
		_Stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.True(_Output.GetButtonState(3));

		_Stick.ReleaseButton(2);
		runtime.ProcessFrame();
		Assert.False(_Output.GetButtonState(3));
	}

	// ── Helpers ─────────────────────────────────────────────────────────

	private IOutputRuntimeContext Build(params IBoundRoute[] routes) =>
		Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			Routes = [..routes],
		});
}
