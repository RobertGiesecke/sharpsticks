namespace ScaledAxisCSharp.InputAbstractions;

public interface IAxisModifier
{
	double Apply(double input,
		IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices);
}
