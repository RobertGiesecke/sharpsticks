namespace ScaledAxisCSharp.InputAbstractions;

public interface IRuntimeAxisModifier
{
	double Apply(double input, JoystickState?[] states);
}