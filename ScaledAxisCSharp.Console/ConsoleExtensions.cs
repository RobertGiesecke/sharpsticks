using ScaledAxisCSharp.Config;

namespace ScaledAxisCSharp.Console;

public static class ConsoleExtensions
{
	extension(Runtime runtime)
	{
		public void RunAsConsole()
		{
			using var cts = new CancellationTokenSource();

			System.Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				// ReSharper disable once AccessToDisposedClosure
				cts.Cancel();
			};

			System.Console.WriteLine($"Running {runtime.Name} profile. Press Ctrl+C to stop.");

			runtime.Run(cts.Token);
		}

		public static void BuildAndRunAsConsole(Runtime.BuildOptions buildOptions)
		{
			using var runtimeMapping = Runtime.Build(buildOptions);
			runtimeMapping.RunAsConsole();
		}
	}
}