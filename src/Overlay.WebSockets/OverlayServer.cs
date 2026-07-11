using System.Collections.Immutable;
using System.Diagnostics;
using Collections.Pooled;

namespace SharpSticks.Overlay.WebSockets;

public sealed class OverlayServer : IOverlayServer
{
	public const ushort DefaultPort = 8787;

	public static readonly OverlayServer Default = new();

	public (OverlayServeResult? serveResult, OverlayServeRuntimeOptions<TInputDevice>? runtimeOptions)
		InferRuntimeOptions<TInputDevice>(
			IJoystickDeviceFactory<TInputDevice> deviceFactory,
			ServeOptions<TInputDevice> options)
		where TInputDevice : JoystickDevice
	{
		var port = options.Port ?? DefaultPort;
		var webRoot = options.WebRoot;

		ImmutableArray<TInputDevice> devices;

		{
			using var enumerated = deviceFactory.EnumerateConnectedInputDevices();
			devices = FilterDevices(enumerated, options.DevicePredicate);
		}

		if (devices.Length == 0)
		{
			return (new() { FailReason = OverlayServeFailReason.NoInputDevices }, null);
		}

		if (webRoot is not null && !Directory.Exists(webRoot))
		{
			return (new() { FailReason = OverlayServeFailReason.WebRootDirectoryNotFound }, null);
		}

		return (null, new()
		{
			WebRoot = webRoot,
			WebRootPath = options.WebRootPath,
			Port = port,
			Devices = devices,
		});
	}

	public OverlayServeResult Serve<TInputDevice>(
		IJoystickDeviceFactory<TInputDevice> deviceFactory,
		ServeOptions<TInputDevice> options,
		CancellationToken cancellationToken)
		where TInputDevice : JoystickDevice
	{
		var tpl = InferRuntimeOptions(deviceFactory, options);
		switch (tpl)
		{
			case { serveResult: { FailReason: not OverlayServeFailReason.None } r }:
				return r;
			case { runtimeOptions: null }:
				return new() { FailReason = OverlayServeFailReason.RuntimeOptionsInferenceFailed };
			case { runtimeOptions.Devices: not { IsDefaultOrEmpty: false } }:
				return new() { FailReason = OverlayServeFailReason.NoInputDevices };
			case { runtimeOptions.WebRoot: { Length: > 0 } webRoot } when !Directory.Exists(webRoot):
			{
				return new() { FailReason = OverlayServeFailReason.WebRootDirectoryNotFound };
			}
		}

		if (tpl.runtimeOptions is not { } runtimeOptions)
		{
			return new() { FailReason = OverlayServeFailReason.RuntimeOptionsInferenceFailed };
		}

		return Serve(runtimeOptions, options.Events, cancellationToken);
	}

	public static OverlayServeResult Serve<TInputDevice>(
		OverlayServeRuntimeOptions<TInputDevice> runtimeOptions,
		OverlayServeEvents<TInputDevice>? events,
		CancellationToken cancellationToken) 
		where TInputDevice : JoystickDevice
	{
		var protocol = OverlayProtocol.Create(runtimeOptions.Devices, version: 1);
		using var server = new OverlayWebSocketServer(runtimeOptions.Port, protocol.Descriptor, runtimeOptions.WebRoot);

		server.Start();

		events?.Started?.Invoke(runtimeOptions);

		try
		{
			RunReadLoop(runtimeOptions.Devices, protocol, server, cancellationToken);
		}
		finally
		{
			foreach (var device in runtimeOptions.Devices)
			{
				device.Dispose();
			}
		}

		return new() { FailReason = OverlayServeFailReason.None };
	}

	private static void RunReadLoop<TInputDevice>(
		ImmutableArray<TInputDevice> devices,
		OverlayProtocol<TInputDevice> protocol,
		OverlayWebSocketServer server, CancellationToken cancellationToken)
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

	private static ImmutableArray<TInputDevice> FilterDevices<TInputDevice>(
		PooledList<TInputDevice> enumerated,
		Func<TInputDevice, bool>? predicate = null)
		where TInputDevice : JoystickDevice
	{
		if (predicate is null)
		{
			return [.. enumerated];
		}

		using var kept = new PooledList<TInputDevice>(enumerated.Count);
		foreach (var device in enumerated)
		{
			if (predicate(device))
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
}