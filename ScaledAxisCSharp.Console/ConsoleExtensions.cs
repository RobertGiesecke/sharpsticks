using System.Runtime.CompilerServices;
using Collections.Pooled;

namespace ScaledAxisCSharp.Console;

public static class ConsoleExtensions
{
	extension(IOutputRuntimeContext runtime)
	{
		public static PooledList<JoystickDevice> EnumerateConnectedDevices()
		{
			using var list = DirectInputJoystickDevice.EnumerateConnected();
			return list.ConvertAll<JoystickDevice>(t => t);
		}

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

		public static void BuildAndRunAsConsole(RuntimeBuilder.BuildOptions buildOptions)
		{
			using var runtimeMapping = Build(buildOptions switch
			{
				{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = VJoyDeviceFactory.Instance },
				_ => buildOptions,
			});
			runtimeMapping.RunAsConsole();
		}

		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext Build(RuntimeBuilder.BuildOptions buildOptions) =>
			RuntimeBuilder.Build(EnsureOutputDeviceFactory(buildOptions));


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext BuildFromConfig(AppConfig config)
		{
			var buildOptions = Runtime.GetBuildOptionsFromConfig(config);
			return Build(EnsureOutputDeviceFactory(buildOptions));
		}
	}

	private static RuntimeBuilder.BuildOptions EnsureOutputDeviceFactory(RuntimeBuilder.BuildOptions buildOptions)
	{
		return buildOptions switch
		{
			{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = VJoyDeviceFactory.Instance },
			_ => buildOptions,
		};
	}
}