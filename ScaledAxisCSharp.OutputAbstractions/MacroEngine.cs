namespace ScaledAxisCSharp.OutputAbstractions;

/// <summary>
/// Drives every <see cref="ButtonMacroRoute"/> across frames: edge-detects the
/// source button, queues OnPress/OnRelease runs per the route's
/// <see cref="MacroReentry"/> policy, steps the active session each frame, and
/// tracks which output buttons are currently held by any session so
/// <see cref="Runtime"/> can OR them into the normal button route output.
/// </summary>
internal sealed class MacroEngine : IDisposable
{
	private readonly ImmutableArray<MacroRouteState> _Routes;
	private readonly ITimeSource _Time;
	private readonly PooledDictionary<OutputButtonBinding, int> _HeldCounts = new();

	public MacroEngine(
		ImmutableArray<ButtonMacroRoute> routes,
		FrozenDictionary<int, int> deviceIndexesById,
		ITimeSource time)
	{
		_Time = time;
		_Routes =
		[
			..routes.Select(r => new MacroRouteState(
				this,
				r,
				deviceIndexesById[r.Binding.DeviceId],
				time.Frequency)),
		];
	}

	public bool IsHeld(OutputButtonBinding button) =>
		_HeldCounts.TryGetValue(button, out var c) && c > 0;

	/// <summary>
	/// Earliest deadline at which the next sleeping macro must be revisited.
	/// Null if no macro is sleeping. Used by <see cref="Runtime.Run"/> to set
	/// the timeout on <see cref="WaitHandle.WaitAny(WaitHandle[], int)"/>.
	/// </summary>
	public long? NextDeadlineTicks { get; private set; }

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

	internal void OnPress(MacroRouteState route, OutputButtonBinding button)
	{
		if (route.HeldButtons.Add(button))
		{
			_HeldCounts[button] = _HeldCounts.GetValueOrDefault(button) + 1;
		}
	}

	internal void OnRelease(MacroRouteState route, OutputButtonBinding button)
	{
		if (route.HeldButtons.Remove(button))
		{
			DecrementHeld(button);
		}
	}

	internal void ReleaseAllForRoute(MacroRouteState route)
	{
		foreach (var button in route.HeldButtons)
		{
			DecrementHeld(button);
		}

		route.HeldButtons.Clear();
	}

	private void DecrementHeld(OutputButtonBinding button)
	{
		var c = _HeldCounts.GetValueOrDefault(button);
		if (c <= 1)
		{
			_HeldCounts.Remove(button);
		}
		else
		{
			_HeldCounts[button] = c - 1;
		}
	}

	public void Dispose()
	{
		foreach (var route in _Routes)
		{
			route.Dispose();
		}

		_HeldCounts.Dispose();
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

	public MacroRouteState(MacroEngine engine, ButtonMacroRoute route, int sourceDeviceIndex, long frequency)
	{
		_Engine = engine;
		Route = route;
		SourceDeviceIndex = sourceDeviceIndex;
		_Frequency = frequency;
	}

	public ButtonMacroRoute Route { get; }
	public int SourceDeviceIndex { get; }
	public bool WasPressedLastFrame;
	public PooledQueue<TriggerKind> Pending { get; } = new(4);
	public PooledSet<OutputButtonBinding> HeldButtons { get; } = new();
	public MacroSession? Running { get; private set; }
	public long? NextDeadlineTicks { get; private set; }

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
					Running.Dispose();
					Running = null;
				}

				Pending.Clear();
				Pending.Enqueue(kind);
				return;
		}
	}

	private void StartFromQueue()
	{
		while (Pending.Count > 0)
		{
			var kind = Pending.Dequeue();
			var actions = kind is TriggerKind.OnPress ? Route.OnPress : Route.OnRelease;
			if (actions.IsDefaultOrEmpty)
			{
				continue;
			}

			Running = new MacroSession(_Engine, this, actions, _Frequency);
			return;
		}
	}

	private void FinishSession()
	{
		if (Running is null)
		{
			return;
		}

		// Normal completion: leave the session's outstanding presses in place
		// so a macro like [Press(X)] holds X across its end. Cancellation paths
		// (CancelAndRestart, engine dispose) DO release — see those call sites.
		Running.Dispose();
		Running = null;
	}

	public void Dispose()
	{
		if (Running is not null)
		{
			Running.Dispose();
			Running = null;
		}

		_Engine.ReleaseAllForRoute(this);
		HeldButtons.Dispose();
		Pending.Dispose();
	}
}

internal sealed class MacroSession : IDisposable, IMacroOutputSink
{
	private readonly MacroEngine _Engine;
	private readonly MacroRouteState _Route;
	private readonly PooledStack<MacroFrame> _CallStack;
	private readonly MacroContext _Ctx;

	public long? NextStepDeadline { get; private set; }

	public MacroSession(MacroEngine engine, MacroRouteState route,
		ImmutableArray<IMacroAction> actions, long frequency)
	{
		_Engine = engine;
		_Route = route;
		_CallStack = new(4);
		_CallStack.Push(new MacroFrame { Actions = actions });
		_Ctx = new MacroContext(this, frequency);
	}

	void IMacroOutputSink.Press(OutputButtonBinding button) => _Engine.OnPress(_Route, button);
	void IMacroOutputSink.Release(OutputButtonBinding button) => _Engine.OnRelease(_Route, button);

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
				_CallStack.Pop();
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

	public void Dispose()
	{
		_CallStack.Dispose();
	}
}

internal sealed class MacroFrame
{
	public required ImmutableArray<IMacroAction> Actions { get; init; }
	public int Cursor;
}
