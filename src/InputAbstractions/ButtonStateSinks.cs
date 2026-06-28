using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>Holds a keyboard key down while asserted (key tap when driven by a Pulse zone).</summary>
internal sealed class KeySink(IInputSynthesizer synthesizer, Key key) : IButtonStateSink
{
    public void SetButtonState(bool pressed)
    {
        if (pressed)
        {
            synthesizer.KeyDown(key);
        }
        else
        {
            synthesizer.KeyUp(key);
        }
    }
}

/// <summary>Holds a synthesized mouse button down while asserted.</summary>
internal sealed class MouseButtonSink(IInputSynthesizer synthesizer, OutputMouseButton button) : IButtonStateSink
{
    public void SetButtonState(bool pressed)
    {
        if (pressed)
        {
            synthesizer.MouseButtonDown(button);
        }
        else
        {
            synthesizer.MouseButtonUp(button);
        }
    }
}

/// <summary>Edge-only sink: a rising edge emits one wheel increment; release is a no-op.</summary>
internal sealed class ScrollSink(IInputSynthesizer synthesizer, ScrollAxis axis, int amount, MouseScrollUnit unit)
    : IButtonStateSink
{
    public void SetButtonState(bool pressed)
    {
        if (!pressed)
        {
            return;
        }

        var (vertical, horizontal) = axis == ScrollAxis.Vertical ? (amount, 0) : (0, amount);
        synthesizer.Scroll(vertical, horizontal, unit);
    }
}
