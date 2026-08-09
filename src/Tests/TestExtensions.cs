namespace SharpSticks.Tests;

public static class TestExtensions
{
	extension(IFakesOutputRuntimeContext context)
	{
		public void ProcessWithDefaultFrameTime()
		{
			context.ProcessFrame(DefaultFrameTime);
		}
	}
	extension(int source)
	{
		public TimeSpan Milliseconds => FromMilliseconds(source);
		public TimeSpan Seconds => FromSeconds(source);
	}

	extension(double source)
	{
		public TimeSpan Milliseconds => FromMilliseconds(source);
		public TimeSpan Seconds => FromSeconds(source);
	}
}