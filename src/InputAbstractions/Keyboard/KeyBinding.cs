namespace SharpSticks.InputAbstractions.Keyboard;

/// <summary>
/// Reads a keyboard key. The keyboard is a singleton, so unlike
/// <see cref="ButtonBinding"/> there is no device id — the key identifies
/// everything.
/// </summary>
public sealed record KeyBinding(Key Key);

/// <summary>
/// Synthesizes a keystroke. Singleton keyboard, so no device id.
/// </summary>
public sealed record OutputKeyBinding(Key Key);
