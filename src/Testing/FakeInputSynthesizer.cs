using SharpSticks.InputAbstractions.Keyboard;
using SharpSticks.InputAbstractions.Mouse;

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
	}

	public readonly record struct Event(EventKind Kind, Key Key = default, OutputMouseButton MouseButton = default);

	private readonly List<Event> _Events = [];

	public IReadOnlyList<Event> Events => _Events;
	public int FlushCount { get; private set; }

	public void KeyDown(Key key) => _Events.Add(new(EventKind.KeyDown, Key: key));
	public void KeyUp(Key key) => _Events.Add(new(EventKind.KeyUp, Key: key));
	public void MouseButtonDown(OutputMouseButton button) => _Events.Add(new(EventKind.MouseButtonDown, MouseButton: button));
	public void MouseButtonUp(OutputMouseButton button) => _Events.Add(new(EventKind.MouseButtonUp, MouseButton: button));
	public void Flush() => FlushCount++;
}
