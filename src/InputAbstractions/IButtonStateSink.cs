namespace SharpSticks.InputAbstractions;

/// <summary>
/// The runtime side of a "button-like output": applies an aggregated pressed/released
/// state to a concrete sink. Built once from an <see cref="IButtonSinkContext"/> (so it
/// caches its device/synthesizer references) and then called every frame the state
/// changes — no per-frame lookup.
///
/// <para>Level sinks (vJoy button, keyboard key, mouse button) act on both edges. The
/// scroll sink is edge-only: <c>SetButtonState(true)</c> emits one wheel increment and
/// <c>SetButtonState(false)</c> is a no-op.</para>
/// </summary>
public interface IButtonStateSink
{
    void SetButtonState(bool pressed);
}
