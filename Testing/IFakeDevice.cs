namespace SharpSticks.Testing;

public interface IFakeDevice
{
	void SetAxisValue(Axis axis, double normalizedValue);
	double GetAxisValue(Axis axis);
	bool GetButtonState(int buttonNumber);
	void SetButtonState(int buttonNumber, bool pressed);
}