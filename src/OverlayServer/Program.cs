using System.Diagnostics;
using Collections.Pooled;
using SharpSticks.OverlayServer;

// SharpSticks.OverlayServer — reads the selected DirectInput devices (physical sticks and,
// since vJoy also enumerates as a DirectInput game controller, the vJoy output) and
// broadcasts their full state to the joyviz overlay over a local binary WebSocket.
//
//   OverlayServer list
//   OverlayServer serve [--port 8787] [--device <name|id|substring>]...
//
// Runs as its own process alongside the routing engine; DirectInput is opened non-exclusive
// so both can read the same devices at once.

const int defaultPort = 8787;

var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "serve";

var deviceFactory = PlatformDefaultInputDevice.Factory;

switch (command)
{
	case "list":
		ListDevices(deviceFactory);
		return 0;
	case "serve":
		return Serve(deviceFactory, args);
	default:
		Console.Error.WriteLine($"Unknown command '{command}'. Use 'list' or 'serve'.");
		return 2;
}

static void ListDevices<TInputDevice>(IJoystickDeviceFactory<TInputDevice> deviceFactory)
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
		Console.WriteLine(
			$"  id={device.DeviceId,-3} axes={device.PhysicalAxes.Length} " +
			$"buttons={device.Capabilities.NumButtons,-3} \"{device.Name}\"");
		device.Dispose();
	}
}

static int Serve<TInputDevice>(IJoystickDeviceFactory<TInputDevice> deviceFactory, string[] args)
	where TInputDevice : JoystickDevice
{
	var port = defaultPort;
	using var selectors = new PooledList<string>();
	string? webRoot = null;
	for (var i = 0; i < args.Length; i++)
	{
		switch (args[i])
		{
			case "--port" when i + 1 < args.Length:
				if (!int.TryParse(args[++i], out port))
				{
					Console.Error.WriteLine("--port expects a number.");
					return 2;
				}

				break;
			case "--device" when i + 1 < args.Length:
				selectors.Add(args[++i]);
				break;
			case "--root" when i + 1 < args.Length:
				webRoot = args[++i];
				break;
			case "serve":
				break;
			default:
				if (args[i].StartsWith('-'))
				{
					Console.Error.WriteLine($"Unknown option '{args[i]}'.");
					return 2;
				}

				break;
		}
	}

	TInputDevice[] devices;

	{
		using var enumerated = deviceFactory.EnumerateConnectedInputDevices();
		devices = SelectDevices(enumerated, selectors);
	}

	if (devices.Length == 0)
	{
		Console.Error.WriteLine("No matching DirectInput devices to serve.");
		return 1;
	}

	if (webRoot is not null && !Directory.Exists(webRoot))
	{
		Console.Error.WriteLine($"--root directory not found: {webRoot}");
		return 2;
	}

	var protocol = OverlayProtocol.Create(devices, version: 1);
	using var server = new OverlayWebSocketServer(port, protocol.Descriptor, webRoot);
	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		// ReSharper disable once AccessToDisposedClosure
		cts.Cancel();
	};

	server.Start();
	Console.WriteLine($"Serving {devices.Length} device(s) on ws://localhost:{port}. Ctrl+C to stop.");
	if (webRoot is not null)
	{
		Console.WriteLine($"Overlay: http://localhost:{port}/joyviz.html   (files from {webRoot})");
	}

	foreach (var device in devices)
	{
		Console.WriteLine(
			$"  \"{device.Name}\" (axes={device.PhysicalAxes.Length}, buttons={device.Capabilities.NumButtons})");
	}

	try
	{
		RunReadLoop(devices, protocol, server, cts.Token);
	}
	finally
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}

	return 0;
}

static TInputDevice[] SelectDevices<TInputDevice>(
	PooledList<TInputDevice> enumerated,
	PooledList<string> selectors)
	where TInputDevice : JoystickDevice
{
	if (selectors.Count == 0)
	{
		return [.. enumerated];
	}

	var kept = new List<TInputDevice>();
	foreach (var device in enumerated)
	{
		var match = selectors.Exists(selector =>
			(int.TryParse(selector, out var id) && id == device.DeviceId) ||
			device.Name.Contains(selector, StringComparison.OrdinalIgnoreCase));
		if (match)
		{
			kept.Add(device);
		}
		else
		{
			device.Dispose();
		}
	}

	return [.. kept];
}

static void RunReadLoop<TInputDevice>(
	TInputDevice[] devices,
	OverlayProtocol<TInputDevice> protocol,
	OverlayWebSocketServer server,
	CancellationToken cancellationToken)
	where TInputDevice : JoystickDevice
{
	var handles = new WaitHandle[devices.Length + 1];
	for (var i = 0; i < devices.Length; i++)
	{
		handles[i] = devices[i].DataAvailable;
	}

	var cancelIndex = devices.Length;
	handles[cancelIndex] = cancellationToken.WaitHandle;

	var states = new JoystickState?[devices.Length];
	var stopwatch = Stopwatch.StartNew();
	const long sendIntervalMs = 15; // ~60 Hz cap
	// Start one interval in the past so the first frame sends immediately. (Must not be
	// long.MinValue: nowMs - long.MinValue overflows negative, disabling all sends.)
	var lastSendMs = -sendIntervalMs;

	while (!cancellationToken.IsCancellationRequested)
	{
		// Wake on any device's new data, or every ~16 ms so held buttons/idle axes and
		// late-joining clients still get fresh frames.
		var index = WaitHandle.WaitAny(handles, 16);
		if (index == cancelIndex)
		{
			break;
		}

		for (var i = 0; i < devices.Length; i++)
		{
			if (devices[i].TryReadState(out var state, out _))
			{
				states[i] = state;
			}
		}

		var nowMs = stopwatch.ElapsedMilliseconds;
		if (nowMs - lastSendMs < sendIntervalMs)
		{
			continue;
		}

		server.Broadcast(protocol.WriteState(states));
		lastSendMs = nowMs;
	}
}