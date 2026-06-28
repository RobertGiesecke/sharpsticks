using System.Runtime.CompilerServices;

namespace SharpSticks.Console;

public static class FactoryExtensions
{
	extension<TInputDevice, TOutputDevice>(ICombinedDeviceFactory<TInputDevice, TOutputDevice> factory)
		where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
		where TOutputDevice : OutputDevice, IOutputDeviceWithFactory<TOutputDevice>
	{
		public static void BuildAndRunAsConsole(
			ConsoleExtensions<TInputDevice, TOutputDevice>.BuildOptions buildOptions,
			DebugLogger? debugLogger = null)
		{
			var effectiveOptions = buildOptions switch
			{
				{ OutputDeviceFactory: null } => buildOptions with
				{
					OutputDeviceFactory = TOutputDevice.Factory,
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

		private static bool TryRunSetupSubcommand(ConsoleExtensions<TInputDevice, TOutputDevice>.BuildOptions options)
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

			var outputButtons = options.Routes.OfType<ButtonToTargetRoute>().Select(route => route.Target)
				.Concat(options.Routes.OfType<AxisZoneRoute>().Select(route => route.Target))
				.OfType<OutputButtonBinding>()
				.ToArray();
			var axisRoutes = options.Routes.OfType<AxisRoute>().ToArray();
			// Macro-only output buttons are discovered by walking IMacroAction.FillOutputs;
			// not extracted here. Setup only needs to validate that buttons declared in
			// direct routes can be created.
			setupCapable.RunSetup(outputButtons, axisRoutes, []);
			return true;
		}


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext<TInputDevice, TOutputDevice> Build(
			ConsoleExtensions<TInputDevice, TOutputDevice>.BuildOptions buildOptions)
		{
			using var connectedDevices = buildOptions.ConnectedDevices is null
				? TInputDevice.Factory.EnumerateConnectedInputDevices()
				: null;

			return RuntimeBuilder.Build(
				EnsureOutputDeviceFactory(
					CopyOptions(
						buildOptions,
						connectedDevices)));
		}

		internal static RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice> CopyOptions(
			ConsoleExtensions<TInputDevice, TOutputDevice>.BuildOptions o,
			PooledList<TInputDevice>? joystickDevices) => new()
		{
			Name = o.Name,
			DebugLogger = o.DebugLogger,
			OutputDeviceFactory = o.OutputDeviceFactory,
			InputSynthesizer = o.InputSynthesizer,
			InitializeInputSynthesizer = o.InitializeInputSynthesizer,
			ConnectedDevices = o.ConnectedDevices ?? [..joystickDevices!],
			Routes = o.Routes,
		};


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext<TInputDevice, TOutputDevice> BuildFromConfig(
			AppConfig config)
		{
			
			var buildOptions = EnsureOutputDeviceFactory(
				Runtime<TInputDevice, TOutputDevice>.GetBuildOptionsFromConfig(config, TOutputDevice.Factory, TInputDevice.Factory));
			return Build<TInputDevice, TOutputDevice>(new()
			{
				Name = buildOptions.Name,
				DebugLogger = buildOptions.DebugLogger,
				OutputDeviceFactory = buildOptions.OutputDeviceFactory,
				ConnectedDevices = buildOptions.ConnectedDevices,
				Routes = buildOptions.Routes,
			});
		}
	}
}