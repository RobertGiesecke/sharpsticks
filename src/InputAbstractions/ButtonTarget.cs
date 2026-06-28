namespace SharpSticks.InputAbstractions;

/// <summary>
/// A digital output a button-like source can drive: a vJoy output button
/// (<see cref="OutputButtonBinding"/>), a keyboard key (<see cref="KeyTarget"/>), a
/// synthesized mouse button (<see cref="MouseButtonTarget"/>), or a scroll-wheel
/// increment (<see cref="ScrollTarget"/>). Each knows how to build a route from a
/// button source (<see cref="CreateRoute"/>) and its runtime apply sink
/// (<see cref="CreateRuntimeSink"/>), so the runtime treats all of them uniformly.
/// </summary>
public abstract record ButtonTarget
{
    /// <summary>Build the route that drives this target from a button source.</summary>
    public abstract IRoute CreateRoute(ButtonBinding source);

    /// <summary>
    /// Build the runtime sink that applies a pressed/released state to this target.
    /// Called once at build time, so the sink can cache its device/synthesizer.
    /// </summary>
    public abstract IButtonStateSink CreateRuntimeSink(IButtonSinkContext context);

    private protected static IInputSynthesizer RequireSynthesizer(IButtonSinkContext context) =>
        context.Synthesizer ?? throw new InvalidOperationException(
            "A keyboard/mouse/scroll target was used, but no IInputSynthesizer was provided to the runtime.");
}

public static class ButtonTargetRoutingExtensions
{
    /// <summary>
    /// Route this button to any <see cref="ButtonTarget"/> — a vJoy button, a keyboard
    /// key, a mouse button, or a scroll increment. The concrete <c>RouteTo</c>/
    /// <c>RouteToKey</c>/<c>RouteToMouse</c>/<c>RouteToScroll</c> overloads are more
    /// convenient when the target type is known.
    /// </summary>
    public static IRoute RouteTo(this ButtonBinding source, ButtonTarget target) => target.CreateRoute(source);
}
