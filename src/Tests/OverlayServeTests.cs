using System.Collections.Concurrent;
using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// End-to-end coverage of <see cref="OverlayServer"/> on fake devices: the
/// fail-fast paths (no devices, missing web root, device predicate), the
/// Starting/Started event order, and a full serve session — a WebSocket client
/// receives the descriptor first, then state frames that track fake input
/// changes, until cancellation shuts the server down cleanly.
/// </summary>
public sealed class OverlayServeTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void InferRuntimeOptions_WithoutDevices_FailsWithNoInputDevices()
	{
		var (result, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			new ListDeviceFactory(), new ServeOptions<FakeJoystickDevice>());

		Assert.Null(runtimeOptions);
		Assert.Equal(OverlayServeFailReason.NoInputDevices, result?.FailReason);
	}

	[Fact]
	public void InferRuntimeOptions_MissingWebRootDirectory_Fails()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();

		var (result, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			new ListDeviceFactory(stick), new ServeOptions<FakeJoystickDevice>
			{
				WebRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
			});

		Assert.Null(runtimeOptions);
		Assert.Equal(OverlayServeFailReason.WebRootDirectoryNotFound, result?.FailReason);
	}

	[Fact]
	public void InferRuntimeOptions_DevicePredicate_KeepsOnlyMatchingDevices()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Fakes.AddInputDevice("Rudder").AddAxis(Axis.Z).Build();

		var (result, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			new ListDeviceFactory([.. _Fakes.InputDevices]), new ServeOptions<FakeJoystickDevice>
			{
				DevicePredicate = device => device.Name == "Stick",
			});

		Assert.Null(result);
		Assert.Equal([stick], runtimeOptions?.Devices ?? []);
	}

	[Fact]
	public void Serve_WithoutDevices_FailsWithoutRaisingEvents()
	{
		var events = new ConcurrentQueue<string>();
		using var cts = new CancellationTokenSource();

		var result = OverlayServer.Default.Serve(
			new ListDeviceFactory(), new ServeOptions<FakeJoystickDevice>
			{
				Events = new()
				{
					Starting = _ => events.Enqueue("starting"),
					Started = _ => events.Enqueue("started"),
				},
			}, cts.Token);

		Assert.Equal(OverlayServeFailReason.NoInputDevices, result.FailReason);
		Assert.Empty(events);
	}

	[Fact]
	public async Task Serve_StreamsFakeDeviceState_UntilCancelled()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();
		var events = new ConcurrentQueue<string>();
		var port = OverlayWire.GetFreePort();

		using var wire = new CancellationTokenSource(OverlayWire.Timeout);
		using var stop = new CancellationTokenSource();
		var serveTask = Task.Run(() => OverlayServer.Serve<FakeJoystickDevice>(new()
		{
			WebRoot = null,
			WebRootPath = null,
			Port = (ushort)port,
			Devices = [stick],
		}, new()
		{
			Starting = _ => events.Enqueue("starting"),
			Started = _ => events.Enqueue("started"),
		}, stop.Token));

		using var ws = await OverlayWire.ConnectAsync(port, wire.Token);

		// Descriptor first: one input device with the fake's shape.
		var descriptor = OverlayFrameReader.ReadDescriptor(
			await OverlayWire.ReceiveBinaryAsync(ws, wire.Token));
		var described = Assert.Single(descriptor.Devices);
		Assert.Equal("Stick", described.Name);
		Assert.Equal([Axis.X], described.Axes);
		Assert.Equal(2, described.ButtonCount);

		// Move the fake input; a state frame reflecting it arrives within the
		// server's ~60 Hz send cadence.
		stick.SetAxisValue(Axis.X, 0.5);
		stick.PressButton(2);
		while (true)
		{
			var state = OverlayFrameReader.ReadState(
				await OverlayWire.ReceiveBinaryAsync(ws, wire.Token), descriptor);
			var device = Assert.Single(state.Devices);
			if (Math.Abs(device.Axes[0] - 0.5) < 1.0 / 32767.0 && device.Buttons[1])
			{
				break;
			}
		}

		stop.Cancel();
		var result = await serveTask;
		Assert.True(result.Success);
		Assert.Equal(["starting", "started"], events.ToArray());
	}

	/// <summary>Hands a fixed device list to code that expects a factory.</summary>
	private sealed class ListDeviceFactory(params FakeJoystickDevice[] devices)
		: IJoystickDeviceFactory<FakeJoystickDevice>
	{
		public PooledList<FakeJoystickDevice> EnumerateConnectedInputDevices()
		{
			var list = new PooledList<FakeJoystickDevice>(devices.Length);
			foreach (var device in devices)
			{
				list.Add(device);
			}

			return list;
		}
	}
}
