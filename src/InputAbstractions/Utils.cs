namespace SharpSticks.InputAbstractions;

public static class Utils
{
	public static T? AsNullable<T>(this T value) where T : struct => value;

	/// <summary>
	/// uses <see cref="NumberFormattingDebugInterpolatedStringHandler"/> to format <see cref="double"/> values without intermediate allocations."/>.
	/// </summary>
	/// <param name="interpolatedStringHandler"></param>
	/// <returns></returns>
	public static string ToDebugString(NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler) =>
		interpolatedStringHandler.ToStringAndClear();

	public static NumberFormattingDebugInterpolatedStringHandler FormatInterpolation(NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler) =>
		interpolatedStringHandler;
}