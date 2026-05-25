namespace SharpSticks.InputAbstractions;

public interface IModifier : IFillDevices;

public interface IModifier<out TRuntimeModifier> : IModifier
{
	TRuntimeModifier CreateModifierRuntimeContext<TInputDevice>(IRuntimeContext<TInputDevice> context)
		where TInputDevice : JoystickDevice;
}