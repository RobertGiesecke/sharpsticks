namespace ScaledAxisCSharp.InputAbstractions;

public interface IRuntimeButtonModifier
{
	bool? Apply(bool input, JoystickState?[] states);
}