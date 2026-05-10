namespace ScaledAxisCSharp.InputAbstractions;

public interface IModifier
{
	void FillDevices(ICollection<int> deviceIds);
}

public interface IModifier<out TRuntimeModifier> : IModifier
{
	TRuntimeModifier CreateModifierRuntimeContext(IRuntimeContext context);
}