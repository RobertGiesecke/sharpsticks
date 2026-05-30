namespace SharpSticks.InputAbstractions;

public static class Utils
{
	public static T? AsNullable<T>(this T value) where T : struct => value;
}