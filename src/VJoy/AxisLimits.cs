namespace SharpSticks.VJoy;

public readonly record struct AxisLimits(int Min, int Max)
{
	public int TranslateSigned(double value)
	{
		var centered = (value + 1.0) * 0.5;
		return Min + (int)Math.Round(centered * (Max - Min));
	}
}