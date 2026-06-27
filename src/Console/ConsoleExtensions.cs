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
		/// OS keyboard/mouse sink for key/mouse macro actions. Leave null to use
		/// the platform default (<see cref="IOutputDeviceWithFactory{TSelf}.DefaultInputSynthesizer"/>);
		/// set it to override or to opt out with a no-op.
		/// </summary>
		public IInputSynthesizer? InputSynthesizer { get; init; }

		/// <summary>
		/// Initialize the synthesizer's backend at startup (the Linux uinput device,
		/// etc.). Default true. Set false on a profile that doesn't synthesize input
		/// to keep the synthetic device from being created.
		/// </summary>
		public bool InitializeInputSynthesizer { get; init; } = true;
		public ImmutableArray<TInputDevice>? ConnectedDevices { get; init; }
		public ImmutableArray<IRoute> Routes { get; init; } = [];
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

	internal static RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice>
		EnsureOutputDeviceFactory<TInputDevice, TOutputDevice>(
			RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice> buildOptions)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice, IOutputDeviceWithFactory<TOutputDevice>
	{
		return buildOptions switch
		{
			{ OutputDeviceFactory: null } => buildOptions with { OutputDeviceFactory = TOutputDevice.Factory },
			_ => buildOptions,
		};
	}
}