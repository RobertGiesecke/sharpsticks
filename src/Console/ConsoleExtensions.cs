using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace SharpSticks.Console;

public static class ConsoleExtensions
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory<PlatformDefaultOutputDevice>? OutputDeviceFactory { get; init; }
		public ImmutableArray<PlatformDefaultInputDevice>? ConnectedDevices { get; init; }
		public ImmutableArray<IBoundRoute> Routes { get; init; } = [];
	}

	public static PooledList<PlatformDefaultInputDevice> EnumerateConnectedDevices() => PlatformDefaultInputDevice.EnumerateConnected();

	extension<TInputDevice, TOutputDevice>(IOutputRuntimeContext<TInputDevice, TOutputDevice> runtime)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public void RunAsConsole(DebugLogger? debugLogger = null)
		{
			using var cts = new CancellationTokenSource();

			System.Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				// ReSharper disable once AccessToDisposedClosure
				cts.Cancel();
			};

			System.Console.WriteLine($"Running {runtime.Name} profile. Press Ctrl+C to stop.");

			runtime.Run(cts.Token, debugLogger);
		}
	}

	extension(IOutputRuntimeContext<PlatformDefaultInputDevice, PlatformDefaultOutputDevice> runtime)
	{
		public static void BuildAndRunAsConsole(BuildOptions buildOptions,
			DebugLogger? debugLogger = null)
		{
			var effectiveOptions = buildOptions switch
			{
				{ OutputDeviceFactory: null } => buildOptions with
				{
					OutputDeviceFactory = PlatformDefaultOutputDeviceFactory.Instance
				},
				_ => buildOptions,
			};

			if (TryRunSetupSubcommand(effectiveOptions))
			{
				return;
			}

			using var runtimeMapping = Build(effectiveOptions);
			runtimeMapping.RunAsConsole(debugLogger);
		}

		private static bool TryRunSetupSubcommand(BuildOptions options)
		{
			var args = Environment.GetCommandLineArgs();
			if (args.Length < 3 || !string.Equals(args[1], "setup", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var subcommand = args[2];
			// ReSharper disable once SuspiciousTypeConversion.Global
			if (options.OutputDeviceFactory is not ISupportsOutputSetup setupCapable)
			{
				System.Console.Error.WriteLine(
					$"The configured output device factory does not support setup subcommands.");
				Environment.Exit(2);
				return true;
			}

			if (!string.Equals(setupCapable.SetupSubcommandName, subcommand, StringComparison.OrdinalIgnoreCase))
			{
				System.Console.Error.WriteLine(
					$"Unknown setup subcommand '{subcommand}'. " +
					$"This factory supports: {setupCapable.SetupSubcommandName}");
				Environment.Exit(2);
				return true;
			}

			var buttonRoutes = options.Routes.OfType<ButtonRoute>().ToArray();
			var axisRoutes = options.Routes.OfType<AxisRoute>().ToArray();
			// Macro-only output buttons are discovered by walking IMacroAction.FillOutputs;
			// not extracted here. Setup only needs to validate that buttons declared in
			// direct routes can be created.
			setupCapable.RunSetup(buttonRoutes, axisRoutes, []);
			return true;
		}


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext<PlatformDefaultInputDevice, PlatformDefaultOutputDevice> Build(
			BuildOptions buildOptions)
		{
			using var connectedDevices = buildOptions.ConnectedDevices is null
				? PlatformDefaultInputDevice.EnumerateConnected()
				: null;

			return RuntimeBuilder.Build(
				EnsureOutputDeviceFactory(
					CopyOptions(
						buildOptions,
						connectedDevices)));
		}

		private static RuntimeBuilder.BuildOptions<PlatformDefaultInputDevice, PlatformDefaultOutputDevice> CopyOptions(
			BuildOptions o,
			PooledList<PlatformDefaultInputDevice>? joystickDevices) => new()
		{
			Name = o.Name,
			DebugLogger = o.DebugLogger,
			OutputDeviceFactory = o.OutputDeviceFactory,
			ConnectedDevices = o.ConnectedDevices ?? [..joystickDevices!],
			Routes = o.Routes,
		};


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext<PlatformDefaultInputDevice, PlatformDefaultOutputDevice> BuildFromConfig(
			AppConfig config)
		{
			var buildOptions = EnsureOutputDeviceFactory(
				Runtime<PlatformDefaultInputDevice, PlatformDefaultOutputDevice>.GetBuildOptionsFromConfig(config));
			return Build(new()
			{
				Name = buildOptions.Name,
				DebugLogger = buildOptions.DebugLogger,
				OutputDeviceFactory = buildOptions.OutputDeviceFactory,
				ConnectedDevices = buildOptions.ConnectedDevices,
				Routes = buildOptions.Routes,
			});
		}
	}

	private static RuntimeBuilder.BuildOptions<PlatformDefaultInputDevice, PlatformDefaultOutputDevice>
		EnsureOutputDeviceFactory(
			RuntimeBuilder.BuildOptions<PlatformDefaultInputDevice, PlatformDefaultOutputDevice> buildOptions)
	{
		return buildOptions switch
		{
			{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = VJoyDeviceFactory.Instance },
			_ => buildOptions,
		};
	}
}