namespace SharpSticks.InputSynthesis.Mouse;

/// <summary>
/// Unit a scroll amount is expressed in. <see cref="Notch"/> is one physical wheel
/// detent (the default); <see cref="HighResolution"/> is 1/120 of a notch, for smooth
/// sub-detent scrolling. Both share the <c>120 = one notch</c> convention, so
/// <see cref="Notch"/> is just <see cref="HighResolution"/> × 120. These are the only
/// units the OS inject layer (Windows <c>SendInput</c> / Linux evdev) can express;
/// "lines" and "pixels" are application-side interpretations and are deliberately absent.
/// </summary>
public enum MouseScrollUnit
{
    Notch = 0,
    HighResolution,
}

/// <summary>Which wheel a scroll route/action drives. Sign of the amount gives the direction.</summary>
public enum ScrollAxis
{
    /// <summary>The vertical wheel. Positive = up / away from the user.</summary>
    Vertical,

    /// <summary>The horizontal (tilt) wheel. Positive = right.</summary>
    Horizontal,
}

/// <summary>
/// A discrete scroll direction for button/macro use. Maps internally to a
/// <see cref="ScrollAxis"/> plus a sign.
/// </summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right,
}
