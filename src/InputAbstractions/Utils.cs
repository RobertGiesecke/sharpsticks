namespace SharpSticks.InputAbstractions;

public static class Utils
{
	public static T? AsNullable<T>(this T value) where T : struct => value;

	public static NumberFormattingDebugInterpolatedStringHandler FormatInterpolation(
		NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler) =>
		interpolatedStringHandler;

	extension(Console)
	{
		/// <summary>
		/// Writes the interpolated string to the <see cref="Console"/> without allocating extra memory.
		/// </summary>
		public static void WriteLineInterpolated(
			scoped NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler)
		{
			Console.WriteLine(interpolatedStringHandler.Text);
			interpolatedStringHandler.Clear();
		}
	}

	extension(TextWriter writer)
	{
		/// <summary>
		/// Writes the interpolated string to the underlying <see cref="TextWriter"/> without allocating extra memory.
		/// </summary>
		public void WriteLineInterpolated(
			scoped NumberFormattingDebugInterpolatedStringHandler interpolatedStringHandler)
		{
			writer.WriteLine(interpolatedStringHandler.Text);
			interpolatedStringHandler.Clear();
		}
	}
}