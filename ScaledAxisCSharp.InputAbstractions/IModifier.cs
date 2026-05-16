namespace ScaledAxisCSharp.InputAbstractions;

public interface IModifier : IFillDevices;

public interface IModifier<out TRuntimeModifier> : IModifier
{
	TRuntimeModifier CreateModifierRuntimeContext(IRuntimeContext context);
}