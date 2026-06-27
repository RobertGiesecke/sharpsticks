using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputSynthesis;

/// <summary>
/// Synthesizes keyboard and mouse events at the OS level — the sink that macro
/// key/mouse actions drive. Deliberately outside the joystick output-device model
/// (no device id, no axis/refcount machinery): the keyboard and mouse are
/// singletons and these events are emitted directly. The implementation is
/// OS-specific (Windows <c>SendInput</c>, Linux uinput) since the BCL has no
/// cross-platform inject primitive; tests use an in-memory fake.
///
/// <para>Down/Up are not refcounted — a macro is responsible for pairing them.
/// Calls may be buffered; <see cref="Flush"/> commits them and is invoked once
/// per frame by the runtime after macros step.</para>
/// </summary>
public interface IInputSynthesizer
{
	void KeyDown(Key key);
	void KeyUp(Key key);
	void MouseButtonDown(OutputMouseButton button);
	void MouseButtonUp(OutputMouseButton button);

	/// <summary>Move the pointer by a relative pixel delta (mouse-space, not absolute).</summary>
	void MoveMouseRelative(int dx, int dy);

	/// <summary>
	/// Eagerly prepare the backend so the first event has no setup cost and any
	/// setup failure surfaces at runtime start rather than mid-session. Idempotent.
	/// The runtime calls this when it starts unless the build opts out; backends
	/// that need no setup (Windows SendInput, fakes) leave it a no-op.
	/// </summary>
	void EnsureInitialized() { }

	/// <summary>Commit any buffered events. No-op for backends that emit eagerly.</summary>
	void Flush();
}
