namespace SharpSticks.InputAbstractions;

/// <summary>
/// How a call to <see cref="IRuntimeInputModifier{TValue}.Apply"/> may
/// interact with the modifier's internal state.
/// </summary>
public enum ApplyMode
{
	/// <summary>
	/// Normal per-frame evaluation — the modifier may advance its internal
	/// state (latched values, integrators, …). Happens exactly once per frame
	/// per modifier instance.
	/// </summary>
	Update,

	/// <summary>
	/// Side-effect-free probe: compute the output without mutating anything
	/// observable. Composing modifiers use this to evaluate a child at a
	/// hypothetical input (e.g. the previous frame's) in addition to the
	/// regular per-frame <see cref="Update"/> call. Implementations must
	/// forward the mode to child modifiers and skip writes to shared objects;
	/// own struct state under
	/// <see cref="StatefulRuntimeInputModifier{TValue,TState}"/> needs no
	/// guarding (probes run on a copy).
	/// </summary>
	Peek,
}
