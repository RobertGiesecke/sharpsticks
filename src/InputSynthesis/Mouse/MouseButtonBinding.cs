namespace SharpSticks.InputSynthesis.Mouse;

/// <summary>
/// Reads a mouse button. The mouse is a singleton, so there is no device id. Uses
/// <see cref="MouseButton"/> (the full readable set, incl. extended buttons).
/// </summary>
public sealed record MouseButtonBinding(MouseButton Button);

/// <summary>
/// Synthesizes a mouse button. Uses the closed <see cref="OutputMouseButton"/>
/// set, since only the standard five can be injected.
/// </summary>
public sealed record OutputMouseButtonBinding(OutputMouseButton Button);
