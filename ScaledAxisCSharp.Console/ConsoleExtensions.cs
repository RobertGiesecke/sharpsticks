using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.Console;

public static class ConsoleExtensions
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory? OutputDeviceFactory { get; init; }
		public ImmutableArray<JoystickDevice>? ConnectedDevices { get; init; }
		public ImmutableArray<IBoundRoute> Routes { get; init; } = [];
	}

	extension(IOutputRuntimeContext runtime)
	{
		public static PooledList<JoystickDevice> EnumerateConnectedDevices()
		{
			using var list = DirectInputJoystickDevice.EnumerateConnected();
			return list.ConvertAll<JoystickDevice>(t => t);
		}

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

		public static void BuildAndRunAsConsole(BuildOptions buildOptions,
			DebugLogger? debugLogger = null)
		{
			using var runtimeMapping = Build(buildOptions switch
			{
				{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = VJoyDeviceFactory.Instance },
				_ => buildOptions,
			});
			runtimeMapping.RunAsConsole(debugLogger);
		}


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext Build(BuildOptions buildOptions)
		{
			using var connectedDevices = buildOptions.ConnectedDevices is null
				? EnumerateConnectedDevices()
				: null;

			return RuntimeBuilder.Build(
				EnsureOutputDeviceFactory(
					CopyOptions(
						buildOptions,
						connectedDevices)));
		}

		private static RuntimeBuilder.BuildOptions CopyOptions(
			BuildOptions o,
			PooledList<JoystickDevice>? joystickDevices) => new()
		{
			Name = o.Name,
			DebugLogger = o.DebugLogger,
			OutputDeviceFactory = o.OutputDeviceFactory,
			ConnectedDevices = o.ConnectedDevices ?? [..joystickDevices!],
			Routes = o.Routes,
		};


		[OverloadResolutionPriority(2)]
		public static IOutputRuntimeContext BuildFromConfig(AppConfig config)
		{
			var buildOptions = EnsureOutputDeviceFactory(Runtime.GetBuildOptionsFromConfig(config));
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

	private static RuntimeBuilder.BuildOptions EnsureOutputDeviceFactory(RuntimeBuilder.BuildOptions buildOptions)
	{
		return buildOptions switch
		{
			{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = VJoyDeviceFactory.Instance },
			_ => buildOptions,
		};
	}
}