namespace SharpSticks.InputAbstractions;

public interface IModifier : IFillDevices;

public interface IModifier<out TRuntimeModifier> : IModifier
{
	TRuntimeModifier CreateModifierRuntimeContext(IRuntimeContext context);
}