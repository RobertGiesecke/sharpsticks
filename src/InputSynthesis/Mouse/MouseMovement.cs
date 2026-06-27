namespace SharpSticks.InputSynthesis.Mouse;

/// <summary>Whether a mouse route/action drives velocity (Relative) or position (Absolute).</summary>
public enum MovementKind
{
	Relative = 0,
	Absolute = 1,
}

/// <summary>Coordinate space an absolute mouse movement maps into.</summary>
public enum MovementSpace
{
	SingleScreen = 0,
	AllScreens = 2,
}

/// <summary>Which pointer axis a mouse route drives.</summary>
public enum MouseDirection
{
	X,
	Y,
}

/// <summary>
/// How a mouse route/action moves the pointer. <see cref="MovementKind.Relative"/>
/// lets the axis control speed; <see cref="MovementKind.Absolute"/> maps the axis to a
/// position within <see cref="Space"/> (for <see cref="MovementSpace.SingleScreen"/>,
/// <see cref="Screen"/> selects the monitor — <see cref="CurrentScreen"/> = the one the
/// cursor is on). Relative ignores <see cref="Space"/> / <see cref="Screen"/>.
/// </summary>
public readonly record struct MouseMovement
{
	/// <summary>Sentinel <see cref="Screen"/> meaning "the monitor the cursor is currently on".</summary>
	public const int CurrentScreen = -1;

	public MouseMovement()
	{
	}

	public required MovementKind Kind { get; init; }
	public MovementSpace Space { get; init; } = MovementSpace.SingleScreen;
	public int Screen { get; init; } = CurrentScreen;

	/// <summary>Relative (velocity) movement; <see cref="Space"/> / <see cref="Screen"/> unused.</summary>
	public static MouseMovement Relative => new() { Kind = MovementKind.Relative };
}
