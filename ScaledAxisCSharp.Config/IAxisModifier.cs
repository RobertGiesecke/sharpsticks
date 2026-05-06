namespace ScaledAxisCSharp.Config;

public interface IAxisModifier
{
	double Apply(double input,
		IReadOnlyDictionary<int, JoystickState> states,
		IReadOnlyDictionary<int, JoystickDevice> devices);
}
