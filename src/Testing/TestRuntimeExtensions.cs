namespace SharpSticks.Testing;

public static class TestRuntimeExtensions
{
	extension(IFakesOutputRuntimeContext context)
	{
		public void ProcessFrame(TimeSpan time)
		{
			context.TimeSource.Advance(time);
			context.ProcessFrame();
		}
	}
}