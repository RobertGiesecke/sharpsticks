using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace SharpSticks.Console;

public static class ConsoleExtensions<TInputDevice, TOutputDevice>
	where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
	where TOutputDevice : OutputDevice, IOutputDeviceWithFactory<TOutputDevice>
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory<TOutputDevice>? OutputDeviceFactory { get; init; }

		/// <summary>
		/// Can be used to register events that run before and after the main run loop.
		/// With support to pass state from the before to the after event
		/// </summary>
		public Func<IOutputRuntimeContext<TInputDevice, TOutputDevice>, ImmutableArray<IRuntimeEventInstance?>>?
			RunEventFactory { get; init; }

		/// <summary>
		/// OS keyboard/mouse sink for key/mouse macro actions. Leave null to use the
		/// output backend's default (<see cref="IOutputDeviceFactory.InputSynthesizer"/>);
		/// set it to override, or to opt out with a no-op.
		/// </summary>
		public IInputSynthesizer? InputSynthesizer { get; init; }

		/// <summary>
		/// Initialize the synthesizer's backend at startup (the Linux uinput device,
		/// etc.). Default true. Set false on a profile that doesn't synthesize input
		/// to keep the synthetic device from being created.
		/// </summary>
		public bool InitializeInputSynthesizer { get; init; } = true;

		public ImmutableArray<TInputDevice>? ConnectedDevices { get; init; }
		public ImmutableArray<IConfigurableRoute> Routes { get; init; } = [];
	}

	public static PooledList<TInputDevice> EnumerateConnectedDevices() =>
		TInputDevice.Factory.EnumerateConnectedInputDevices();

	public static void BuildAndRunAsConsole(BuildOptions buildOptions,
		DebugLogger? debugLogger = null)
	{
		// ReSharper disable once InvokeAsExtensionMember
		FactoryExtensions.BuildAndRunAsConsole<TInputDevice, TOutputDevice>(buildOptions, debugLogger);
	}

	[OverloadResolutionPriority(2)]
	public static IOutputRuntimeContext<TInputDevice, TOutputDevice> BuildRuntime(
		BuildOptions buildOptions)
	{
		using var connectedDevices = buildOptions.ConnectedDevices is null
			? TInputDevice.Factory.EnumerateConnectedInputDevices()
			: null;

		return RuntimeBuilder.Build(
			EnsureOutputDeviceFactory(
				FactoryExtensions.CopyOptions(
					buildOptions,
					connectedDevices)));
	}
}

public static class ConsoleExtensions
{
	extension<TInputDevice, TOutputDevice>(IOutputRuntimeContext<TInputDevice, TOutputDevice> runtime)
		where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
		where TOutputDevice : OutputDevice
	{
		public void RunAsConsole(
			DebugLogger? debugLogger = null,
			IReadOnlyCollection<IRuntimeEventInstance>? runtimeEventInstances = null)
		{
			using var cts = new CancellationTokenSource();

			System.Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				// ReSharper disable once AccessToDisposedClosure
				cts.Cancel();
			};

			System.Console.WriteLine($"Running {runtime.Name} profile. Press Ctrl+C to stop.");

			using PooledList<IInitializedOnAfterRunEvent>? initializedOnAfterRunEvents = runtimeEventInstances switch
			{
				{ Count: > 0 } => new(runtimeEventInstances.Count, ClearMode.Always),
				_ => null,
			};
			var runStarted = false;
			var runFailed = false;
			try
			{
				if (initializedOnAfterRunEvents is not null)
				{
					var beforeRunArgs = new RuntimeEventInstance.BeforeRunArgs
					{
						Runtime = runtime,
					};

					foreach (var runtimeEventInstance in runtimeEventInstances!)
					{
						if (runtimeEventInstance.BeforeRun(beforeRunArgs, cts.Token) is { } onBeforeRun)
						{
							initializedOnAfterRunEvents.Add(onBeforeRun);
						}
					}
				}

				runStarted = true;
				try
				{
					runtime.Run(cts.Token, debugLogger);
				}
				catch
				{
					runFailed = true;
					throw;
				}
			}
			finally
			{
				cts.Cancel();

				if (initializedOnAfterRunEvents is { Count: > 0 })
				{
					var args = new AfterRunArgs
					{
						RunStarted = runStarted,
						RunFailed = runFailed,
					};

					// when BeforeRun or Run threw, that exception is already unwinding through
					// this finally — after-run failures must not replace it (or stop the loop)
					var primaryExceptionInFlight = !runStarted || runFailed;
					using PooledList<Exception>? afterRunExceptions =
						primaryExceptionInFlight ? null : new(ClearMode.Always);

					for (var index = initializedOnAfterRunEvents.Count - 1; index >= 0; index--)
					{
						var onAfterRunEvent = initializedOnAfterRunEvents[index];
						try
						{
							onAfterRunEvent.OnAfterRun(args, cts.Token);
						}
						catch (Exception exception) when (afterRunExceptions is not null)
						{
							afterRunExceptions.Add(exception);
						}
						catch (Exception exception)
						{
							System.Console.Error.WriteLine($"After-run event failed during shutdown: {exception}");
						}
					}

					if (afterRunExceptions is { Count: > 0 })
					{
						throw new AggregateException(afterRunExceptions);
					}
				}
			}
		}
	}

	internal static RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice>
		EnsureOutputDeviceFactory<TInputDevice, TOutputDevice>(
			RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice> buildOptions)
		where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
		where TOutputDevice : OutputDevice, IOutputDeviceWithFactory<TOutputDevice>
	{
		return buildOptions switch
		{
			{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = TOutputDevice.Factory },
			_ => buildOptions,
		};
	}
}