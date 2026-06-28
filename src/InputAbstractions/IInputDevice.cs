namespace SharpSticks.InputAbstractions;

/// <summary>
/// Marker for any input source kind the runtime can read — joystick, keyboard,
/// or mouse. It is deliberately empty: only joysticks are enumerated and
/// addressed by a numeric id (<see cref="IJoystickDevice.DeviceId"/>), while
/// keyboard and mouse are singletons identified by their binding type, not an
/// id. The per-frame read contract differs by kind (each produces its own
/// state shape), so it lives on the concrete device classes, not here.
/// </summary>
public interface IInputDevice;
