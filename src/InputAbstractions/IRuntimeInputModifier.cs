namespace SharpSticks.InputAbstractions;

public interface IRuntimeInputModifier<TValue>
{
	/// <summary>
	/// Transform <paramref name="input"/> given the current device states.
	/// With <see cref="ApplyMode.Update"/> (the default) the modifier may
	/// advance its internal state; with <see cref="ApplyMode.Peek"/> it must
	/// not mutate anything observable. The mode must always be passed along
	/// to child modifiers — a composer cannot know whether its children are
	/// stateful. Pure modifiers without children simply ignore it; modifiers
	/// with their own per-frame state should derive from
	/// <see cref="StatefulRuntimeInputModifier{TValue,TState}"/>, which makes
	/// the peek contract hard to get wrong.
	/// </summary>
	TValue Apply(TValue input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update);
}