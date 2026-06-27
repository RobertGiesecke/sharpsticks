using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.Testing;

/// <summary>
/// In-memory <see cref="IInputSynthesizer"/> for deterministic tests: records
/// every synthesized event in order and counts <see cref="Flush"/> calls, so a
/// test can assert exactly what a macro emitted without touching the OS.
/// </summary>
public sealed class FakeInputSynthesizer : IInputSynthesizer
{
	public enum EventKind
	{
		KeyDown,
		KeyUp,
		MouseButtonDown,
		MouseButtonUp,
		MouseMove,
	}

	public readonly record struct Event(
		EventKind Kind,
		Key Key = default,
		OutputMouseButton MouseButton = default,
		int Dx = 0,
		int Dy = 0);

	private readonly List<Event> _Events = [];

	public IReadOnlyList<Event> Events => _Events;
	public int FlushCount { get; private set; }

	public void KeyDown(Key key) => _Events.Add(new(EventKind.KeyDown, Key: key));
	public void KeyUp(Key key) => _Events.Add(new(EventKind.KeyUp, Key: key));
	public void MouseButtonDown(OutputMouseButton button) => _Events.Add(new(EventKind.MouseButtonDown, MouseButton: button));
	public void MouseButtonUp(OutputMouseButton button) => _Events.Add(new(EventKind.MouseButtonUp, MouseButton: button));
	public void MoveMouseRelative(int dx, int dy) => _Events.Add(new(EventKind.MouseMove, Dx: dx, Dy: dy));
	public void Flush() => FlushCount++;
}
