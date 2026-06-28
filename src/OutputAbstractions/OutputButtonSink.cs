namespace SharpSticks.OutputAbstractions;

/// <summary>
/// The <see cref="IButtonStateSink"/> for an output-device button. Nothing here is
/// device-type-specific — it's the same generic <see cref="OutputDevice.SetButtonState"/>
/// call the runtime has always made, polymorphic over any <see cref="OutputDevice"/>
/// (vJoy, uinput, fake). It lives in the output layer only because that's where
/// <see cref="OutputDevice"/> is; the runtime builds it via
/// <see cref="IButtonSinkContext.CreateOutputButtonSink"/>.
///
/// <para>Aggregation (presser/suppressor counting across every source) stays in the
/// runtime — this just applies the final pressed/released state.</para>
/// </summary>
internal sealed class OutputButtonSink(OutputDevice device, int buttonNumber) : IButtonStateSink
{
    public void SetButtonState(bool pressed) => device.SetButtonState(buttonNumber, pressed);
}
