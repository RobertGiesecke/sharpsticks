// SharpSticks.Overlay.Server — reads the selected DirectInput devices and broadcasts
// their full state to the joyviz overlay over a local binary WebSocket.
//
//   OverlayServer list
//   OverlayServer serve [--port 8787] [--device <name|id|substring>]... [--include-outputs]
//
// Runs as its own process alongside the routing engine; DirectInput is opened non-exclusive
// so both can read the same physical devices at once. Virtual output devices (vJoy) are
// skipped by default — holding their DirectInput mirror open blocks the routing engine
// from feeding them. Pass --include-outputs to serve them anyway (e.g. when no engine
// is running); the in-process ServeOverlay integration serves them safely instead.

namespace SharpSticks.Overlay.Server;

public static class Program
{
	public static int Main(string[] args)
	{
		var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "serve";

		var deviceFactory = PlatformDefaultInputDevice.Factory;

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			// ReSharper disable once AccessToDisposedClosure
			cts.Cancel();
		};

		switch (command)
		{
			case "list":
				ListDevices(deviceFactory);
				return 0;
			case "serve":
				return Serve(deviceFactory, args, cts.Token);
			default:
				Console.Error.WriteLine($"Unknown command '{command}'. Use 'list' or 'serve'.");
				return 2;
		}
	}

	public static int Serve<TInputDevice>(
		IJoystickDeviceFactory<TInputDevice> deviceFactory,
		string[] args,
		CancellationToken cancellationToken)
		where TInputDevice : JoystickDevice
	{
		if (ServeOptions<TInputDevice>.BuildServeOptionsFromArgs(args) is not { } serveOptions)
		{
			return 2;
		}

		serveOptions = serveOptions with
		{
			Events = new()
			{
				Started = OverlayUtils.ShowServerStatus,
			},
		};

		OverlayServeResult serveResult;
		try
		{
			serveResult = OverlayServer.Default.Serve(deviceFactory, serveOptions, cancellationToken);
		}
		catch (TaskCanceledException)
		{
			Console.Error.WriteLine("cancelled");
			throw;
		}

		switch (serveResult)
		{
			case { FailReason: OverlayServeFailReason.None }:
				return 0;
			case { FailReason: OverlayServeFailReason.NoInputDevices }:
				Console.Error.WriteLine("No matching DirectInput devices to serve.");
				return 2;
			case { FailReason: OverlayServeFailReason.WebRootDirectoryNotFound }:
				Console.Error.WriteLine($"--root directory not found: {serveOptions.WebRoot}");
				return 2;
			case var result:
				Console.Error.WriteLine($"{nameof(Serve)} failed with result {result}");
				return 2;
		}
	}

	public static void ListDevices<TInputDevice>(IJoystickDeviceFactory<TInputDevice> deviceFactory)
		where TInputDevice : JoystickDevice
	{
		using var devices = deviceFactory.EnumerateConnectedInputDevices();
		if (devices.Count == 0)
		{
			Console.WriteLine("No DirectInput devices found.");
			return;
		}

		Console.WriteLine($"{devices.Count} device(s):");
		foreach (var device in devices)
		{
			var outputMarker = device.IsVirtualOutputMirror
				? " (virtual output; served only with --include-outputs)"
				: "";
			Console.WriteLine(
				$"  id={device.DeviceId,-3} axes={device.PhysicalAxes.Length} " +
				$"buttons={device.Capabilities.NumButtons,-3} \"{device.Name}\"{outputMarker}");
			device.Dispose();
		}
	}
}
