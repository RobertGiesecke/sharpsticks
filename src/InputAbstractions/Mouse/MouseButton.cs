namespace SharpSticks.InputAbstractions.Mouse;

/// <summary>
/// Any mouse button readable as input — the value type input bindings carry.
/// <see cref="Index"/> is the 1-based HID button usage (Button page 0x09), so the
/// standard five (<see cref="NamedMouseButton"/>, which converts implicitly) plus
/// any extended button a gaming mouse exposes are all representable. The output
/// side is deliberately a different, smaller type (<see cref="OutputMouseButton"/>)
/// because only the standard five can be synthesized via the OS — reading is a
/// superset of writing, so the asymmetry is encoded in the types.
/// </summary>
public readonly record struct MouseButton(int Index)
{
	public static implicit operator MouseButton(NamedMouseButton button) => new((int)button);

	public override string ToString() =>
		Enum.IsDefined((NamedMouseButton)Index)
			? ((NamedMouseButton)Index).ToString()
			: $"MouseButton({Index})";
}

/// <summary>
/// The standard mouse buttons with friendly names, as 1-based HID button usages.
/// Converts implicitly to <see cref="MouseButton"/>. Extended buttons (6+) have no
/// name — reach them via <c>new MouseButton(n)</c>.
/// </summary>
public enum NamedMouseButton
{
	/// <summary>HID button 1 — left / primary.</summary>
	Left = 1,
	/// <summary>HID button 2 — right / secondary.</summary>
	Right = 2,
	/// <summary>HID button 3 — middle / wheel click.</summary>
	Middle = 3,
	/// <summary>HID button 4 — X1 / "back" side button.</summary>
	X1 = 4,
	/// <summary>HID button 5 — X2 / "forward" side button.</summary>
	X2 = 5,
}

/// <summary>
/// The mouse buttons that can be <em>synthesized</em> as output. Deliberately
/// closed to the five the OS can inject (Windows <c>SendInput</c>, Linux uinput):
/// extended buttons (6+) can be read but not emitted, so they're absent here and
/// "route to button 6" simply won't compile.
/// </summary>
public enum OutputMouseButton
{
	/// <summary>Left / primary.</summary>
	Left = 1,
	/// <summary>Right / secondary.</summary>
	Right = 2,
	/// <summary>Middle / wheel click.</summary>
	Middle = 3,
	/// <summary>X1 / "back" side button.</summary>
	X1 = 4,
	/// <summary>X2 / "forward" side button.</summary>
	X2 = 5,
}
