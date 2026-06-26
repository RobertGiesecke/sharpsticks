using System.Runtime.CompilerServices;

namespace SharpSticks.OutputAbstractions;

/// <summary>
/// Drives every <see cref="ButtonMacroRoute"/> across frames: edge-detects the
/// source button, queues OnPress/OnRelease runs per the route's
/// <see cref="MacroReentry"/> policy, steps the active session each frame, and
/// pumps Press/Release events through the <see cref="Runtime"/>-supplied
/// pressers / suppressors callbacks. The engine itself owns no held-state — the
/// counts live on <see cref="Runtime"/>'s <c>OutputButtonWithBindings</c>.
/// </summary>
internal sealed class MacroEngine : IDisposable
{
	private readonly ImmutableArray<MacroRouteState> _Routes;
	private readonly ITimeSource _Time;
	public IRuntimeContext RuntimeContext { get; }
	private readonly HandleOutputButtonBinding _IncPress;
	private readonly HandleOutputButtonBinding _DecPress;
	private readonly HandleOutputButtonBinding _IncSuppress;
	private readonly HandleOutputButtonBinding _DecSuppress;
	private readonly PooledStack<MacroSession> _SessionPool;

	public delegate void HandleOutputButtonBinding(OutputButtonStateIndex button);
	
	public MacroEngine(
		ImmutableArray<ButtonMacroRoute> routes,
		FrozenDictionary<int, int> deviceIndexesById,
		IRuntimeContext runtimeContext,
		HandleOutputButtonBinding incrementPressers,
		HandleOutputButtonBinding decrementPressers,
		HandleOutputButtonBinding incrementSuppressors,
		HandleOutputButtonBinding decrementSuppressors)
	{
		_Time = runtimeContext.TimeSource;
		RuntimeContext = runtimeContext;
		_IncPress = incrementPressers;
		_DecPress = decrementPressers;
		_IncSuppress = incrementSuppressors;
		_DecSuppress = decrementSuppressors;
		_Routes =
		[
			..routes.Select(r => new MacroRouteState(
				this,
				r,
				deviceIndexesById[r.Binding.DeviceId],
				_Time.Frequency)),
		];

		// Each route runs at most one session at a time (its single Running field;
		// the reentry policies queue/drop/cancel rather than parallelize), so the
		// pool can drain to at most one borrow per route. Pre-seeding exactly that
		// many makes RentSession exhaustion-proof — it never allocates in the hot
		// path — without over-allocating.
		var poolSize = _Routes.Length;
		_SessionPool = new(poolSize);
		for (var i = 0; i < poolSize; i++)
		{
			_SessionPool.Push(new(this, _Time.Frequency));
		}
	}

	internal MacroSession RentSession() =>
		_SessionPool.Count > 0
			? _SessionPool.Pop()
			: new(this, _Time.Frequency);

	internal void ReturnSession(MacroSession session)
	{
		session.Recycle();
		_SessionPool.Push(session);
	}

	/// <summary>
	/// Earliest deadline at which the next sleeping macro must be revisited.
	/// Null if no macro is sleeping. Used by <see cref="Runtime.Run"/> to set
	/// the timeout on <see cref="WaitHandle.WaitAny(WaitHandle[], int)"/>.
	/// </summary>
	public long? NextDeadlineTicks { get; private set; }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void Step(JoystickState?[] states)
	{
		var now = _Time.GetTimestamp();
		long? earliest = null;
		foreach (var route in _Routes)
		{
			route.Step(states, now);
			if (route.NextDeadlineTicks is { } d && (earliest is null || d < earliest))
			{
				earliest = d;
			}
		}

		NextDeadlineTicks = earliest;
	}

	/// <summary>
	/// Called by <see cref="Runtime"/> on a route group's falling edge: any
	/// macro that has the button latched as "released-override" drops the
	/// override so the next route press can assert again.
	/// </summary>
	public void ClearSuppressionFor(OutputButtonStateIndex button)
	{
		foreach (var route in _Routes)
		{
			if (route.ReleasedButtons.Remove(button))
			{
				_DecSuppress(button);
			}
		}
	}

	internal void OnPress(MacroRouteState route, OutputButtonStateIndex button)
	{
		if (route.ReleasedButtons.Remove(button))
		{
			_DecSuppress(button);
		}

		if (route.PressedButtons.Add(button))
		{
			_IncPress(button);
		}
	}

	internal void OnRelease(MacroRouteState route, OutputButtonStateIndex button)
	{
		if (route.PressedButtons.Remove(button))
		{
			_DecPress(button);
		}

		if (route.ReleasedButtons.Add(button))
		{
			_IncSuppress(button);
		}
	}

	internal void ReleaseAllForRoute(MacroRouteState route)
	{
		foreach (var button in route.PressedButtons)
		{
			_DecPress(button);
		}

		route.PressedButtons.Clear();

		foreach (var button in route.ReleasedButtons)
		{
			_DecSuppress(button);
		}

		route.ReleasedButtons.Clear();
	}

	public void Dispose()
	{
		foreach (var route in _Routes)
		{
			route.Dispose();
		}

		while (_SessionPool.Count > 0)
		{
			_SessionPool.Pop().Dispose();
		}

		_SessionPool.Dispose();
	}
}

internal enum TriggerKind
{
	OnPress,
	OnRelease,
}

internal enum SessionStepResult
{
	Finished,
	Waiting,
}

internal sealed class MacroRouteState : IDisposable
{
	private readonly MacroEngine _Engine;
	private readonly long _Frequency;
	private readonly ImmutableArray<IRuntimeMacroAction> _OnPress;
	private readonly ImmutableArray<IRuntimeMacroAction> _OnRelease;

	public MacroRouteState(
		MacroEngine engine,
		ButtonMacroRoute route,
		int sourceDeviceIndex,
		long frequency)
	{
		_Engine = engine;
		Route = route;
		SourceDeviceIndex = sourceDeviceIndex;
		_Frequency = frequency;

		_OnPress = CreateRuntimeActions(route.OnPress);
		_OnRelease = CreateRuntimeActions(route.OnRelease);

		// A route can press/release at most the distinct output buttons its
		// actions can ever touch. Pre-size both sets to that bound so their
		// backing is allocated here at build time, never lazily on the first
		// macro fire inside the per-frame hot path.
		var maxButtons = Math.Max(1, CountDistinctOutputs(route));
		PressedButtons = new(maxButtons);
		ReleasedButtons = new(maxButtons);
	}

	private static int CountDistinctOutputs(ButtonMacroRoute route)
	{
		using var outputs = new PooledSet<OutputButtonBinding>();
		FillOutputs(route.OnPress, outputs);
		FillOutputs(route.OnRelease, outputs);
		return outputs.Count;

		static void FillOutputs(ImmutableArray<IMacroAction> actions, ICollection<OutputButtonBinding> sink)
		{
			if (actions.IsDefaultOrEmpty)
			{
				return;
			}

			foreach (var action in actions)
			{
				action.FillOutputs(sink);
			}
		}
	}

	private ImmutableArray<IRuntimeMacroAction> CreateRuntimeActions(ImmutableArray<IMacroAction> actions)
	{
		if (actions.IsDefaultOrEmpty)
		{
			return [];
		}
		using var list = new PooledList<IRuntimeMacroAction>(actions.Length);
		foreach (var macroAction in actions)
		{
			list.Add(macroAction.CreateRuntimeAction(_Engine.RuntimeContext));
		}

		return [..list.Span];
	}

	public ButtonMacroRoute Route { get; }
	public int SourceDeviceIndex { get; }
	public bool WasPressedLastFrame;
	public PooledQueue<TriggerKind> Pending { get; } = new(4);

	/// <summary>Buttons this route currently asserts via <c>Press</c>.</summary>
	public PooledSet<OutputButtonStateIndex> PressedButtons { get; }

	/// <summary>Buttons this route currently force-releases via <c>Release</c>.</summary>
	public PooledSet<OutputButtonStateIndex> ReleasedButtons { get; }

	public MacroSession? Running { get; private set; }
	public long? NextDeadlineTicks { get; private set; }

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void Step(JoystickState?[] states, long now)
	{
		if (states[SourceDeviceIndex] is { } state)
		{
			var isPressed = state.IsButtonPressed(Route.Binding.ButtonNumber);
			if (isPressed && !WasPressedLastFrame)
			{
				HandleTrigger(TriggerKind.OnPress);
			}
			else if (!isPressed && WasPressedLastFrame)
			{
				HandleTrigger(TriggerKind.OnRelease);
			}

			WasPressedLastFrame = isPressed;
		}

		if (Running is null && Pending.Count > 0)
		{
			StartFromQueue();
		}

		while (Running is not null)
		{
			var result = Running.Step(now);
			if (result is SessionStepResult.Finished)
			{
				FinishSession();
				if (Running is null && Pending.Count > 0)
				{
					StartFromQueue();
				}

				continue;
			}

			break;
		}

		NextDeadlineTicks = Running?.NextStepDeadline;
	}

	private void HandleTrigger(TriggerKind kind)
	{
		switch (Route.Reentry)
		{
			case MacroReentry.QueueUntilDone:
				Pending.Enqueue(kind);
				return;
			case MacroReentry.DropIfBusy:
				if (Running is null)
				{
					Pending.Enqueue(kind);
				}

				return;
			case MacroReentry.CancelAndRestart:
				if (Running is not null)
				{
					_Engine.ReleaseAllForRoute(this);
					_Engine.ReturnSession(Running);
					Running = null;
				}

				Pending.Clear();
				Pending.Enqueue(kind);
				return;
		}
	}

	private void StartFromQueue()
	{
		while (Pending.TryDequeue(out var kind))
		{
			var actions = kind is TriggerKind.OnPress ? _OnPress : _OnRelease;
			if (actions.IsDefaultOrEmpty)
			{
				continue;
			}

			Running = _Engine.RentSession();
			Running.Reset(this, actions);
			return;
		}
	}

	private void FinishSession()
	{
		if (Running is null)
		{
			return;
		}

		// Normal completion leaves the route's outstanding presses + suppressions
		// in place so a macro like [Press(X)] holds X across its end. Cancellation
		// paths (CancelAndRestart, engine dispose) DO release — see those call sites.
		_Engine.ReturnSession(Running);
		Running = null;
	}

	public void Dispose()
	{
		if (Running is not null)
		{
			_Engine.ReturnSession(Running);
			Running = null;
		}

		_Engine.ReleaseAllForRoute(this);
		PressedButtons.Dispose();
		ReleasedButtons.Dispose();
		Pending.Dispose();
	}
}

internal sealed class MacroSession : IDisposable, IMacroOutputSink
{
	private readonly MacroEngine _Engine;
	private readonly PooledStack<MacroFrame> _CallStack = new(4);
	private readonly PooledStack<MacroFrame> _FrameFreePool = new(4);
	private readonly MacroContext _Ctx;
	private MacroRouteState? _Route;

	public long? NextStepDeadline { get; private set; }

	public MacroSession(MacroEngine engine, long frequency)
	{
		_Engine = engine;
		_Ctx = new(this, frequency);
		// Pre-seed one MacroFrame so the very first Reset (which pushes one frame
		// per current macro semantics) never has to allocate. Nested macros that
		// push deeper would still allocate the extra frames on first nesting.
		_FrameFreePool.Push(new());
	}

	public void Reset(MacroRouteState route, ImmutableArray<IRuntimeMacroAction> actions)
	{
		_Route = route;
		NextStepDeadline = null;
		PushFrame(actions);
	}

	/// <summary>
	/// Returns the session to its idle state — frames go back to the per-session
	/// free pool, the route binding is cleared — so the next <see cref="Reset"/>
	/// reuses the same instance without allocating.
	/// </summary>
	public void Recycle()
	{
		while (_CallStack.Count > 0)
		{
			_FrameFreePool.Push(_CallStack.Pop());
		}

		_Route = null;
		NextStepDeadline = null;
	}

	void IMacroOutputSink.Press(OutputButtonStateIndex button) => _Engine.OnPress(_Route!, button);
	void IMacroOutputSink.Release(OutputButtonStateIndex button) => _Engine.OnRelease(_Route!, button);

	public SessionStepResult Step(long now)
	{
		if (NextStepDeadline is { } deadline && now < deadline)
		{
			return SessionStepResult.Waiting;
		}

		NextStepDeadline = null;

		while (true)
		{
			if (_CallStack.Count == 0)
			{
				return SessionStepResult.Finished;
			}

			var frame = _CallStack.Peek();
			if (frame.Cursor >= frame.Actions.Length)
			{
				_FrameFreePool.Push(_CallStack.Pop());
				continue;
			}

			var action = frame.Actions[frame.Cursor];
			_Ctx.Refresh(now);
			var status = action.Step(_Ctx);

			switch (status.Kind)
			{
				case MacroStatusKind.Done:
					frame.Cursor++;
					continue;
				case MacroStatusKind.RunAgainNextFrame:
					return SessionStepResult.Waiting;
				case MacroStatusKind.WaitUntil:
					frame.Cursor++;
					NextStepDeadline = status.DeadlineTicks;
					return SessionStepResult.Waiting;
			}
		}
	}

	private void PushFrame(ImmutableArray<IRuntimeMacroAction> actions)
	{
		MacroFrame frame;
		if (_FrameFreePool.Count > 0)
		{
			frame = _FrameFreePool.Pop();
		}
		else
		{
			frame = new();
		}

		frame.Actions = actions;
		frame.Cursor = 0;
		_CallStack.Push(frame);
	}

	public void Dispose()
	{
		_CallStack.Dispose();
		_FrameFreePool.Dispose();
	}
}

internal sealed class MacroFrame
{
	public ImmutableArray<IRuntimeMacroAction> Actions;
	public int Cursor;
}
