using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// A digital output a source button can be routed to: a vJoy output button
/// (<see cref="OutputButtonBinding"/>), a synthesized mouse button
/// (<see cref="MouseButtonTarget"/>), or a scroll-wheel increment
/// (<see cref="ScrollTarget"/>). Lets <c>button.RouteTo(target)</c> work uniformly —
/// each target knows how to build its own route.
/// </summary>
public interface IButtonTarget
{
    IRoute CreateRoute(ButtonBinding source);
}

/// <summary>A synthesized mouse button as an <see cref="IButtonTarget"/>.</summary>
public readonly record struct MouseButtonTarget(OutputMouseButton Button) : IButtonTarget
{
    public IRoute CreateRoute(ButtonBinding source) =>
        new ButtonToMouseRoute { Source = source, Button = Button };
}

public static class ButtonTargetRoutingExtensions
{
    /// <summary>
    /// Route this button to any <see cref="IButtonTarget"/> — a vJoy button, a mouse
    /// button, or a scroll increment. The concrete <c>RouteTo</c>/<c>RouteToMouse</c>/
    /// <c>RouteToScroll</c> overloads are more convenient when the target type is known.
    /// </summary>
    public static IRoute RouteTo(this ButtonBinding source, IButtonTarget target) => target.CreateRoute(source);
}
