namespace SharpSticks.InputAbstractions;

/// <summary>
/// Base for modifiers with per-frame state. All mutable state lives in the
/// <typeparamref name="TState"/> struct handed by ref to the core overload:
/// <see cref="ApplyMode.Update"/> calls it with the instance's state,
/// <see cref="ApplyMode.Peek"/> with a throwaway copy — so a derived class is
/// structurally incapable of corrupting its own state on a peek and can write
/// to <c>state</c> unconditionally.
///
/// The mode is still passed to the core for everything the struct copy cannot
/// cover: forward it when evaluating child modifiers, and guard mutations of
/// shared objects (anything reachable outside <c>state</c>) with it.
/// Self-contained modifiers can ignore it.
/// </summary>
public abstract record StatefulRuntimeInputModifier<TValue, TState> : IRuntimeInputModifier<TValue>
	where TState : struct
{
	private TState _State;

	protected StatefulRuntimeInputModifier()
	{
	}

	protected StatefulRuntimeInputModifier(TState state)
	{
		_State = state;
	}

	/// <summary>The committed state, e.g. for debug views.</summary>
	protected ref readonly TState State => ref _State;

	protected abstract TValue Apply(TValue input, JoystickState?[] states, ref TState state, ApplyMode applyMode);

	public TValue Apply(TValue input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
	{
		if (applyMode == ApplyMode.Peek)
		{
			var copy = _State;
			return Apply(input, states, ref copy, applyMode);
		}

		return Apply(input, states, ref _State, applyMode);
	}
}