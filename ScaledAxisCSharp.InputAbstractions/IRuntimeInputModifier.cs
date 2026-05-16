namespace ScaledAxisCSharp.InputAbstractions;

public interface IRuntimeInputModifier<TValue>
{
	TValue Apply(TValue input, JoystickState?[] states);
}