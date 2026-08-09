using System.Net;
using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// Wire-level coverage of the dependency-free WebSocket server over real
/// loopback sockets: the descriptor is the first frame every client sees,
/// broadcasts reach every client and silently drop dead ones, and the static
/// file path serves the web root only — missing files and escaped path
/// traversal get a 404.
/// </summary>
public sealed class OverlayWebSocketServerTests
{
	[Fact]
	public async Task Connect_ReceivesTheDescriptorFirst()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		byte[] descriptor = [0x01, 42, 0];
		var port = OverlayWire.GetFreePort();
		using var server = new OverlayWebSocketServer(port, descriptor);
		server.Start();

		using var ws = await OverlayWire.ConnectAsync(port, cts.Token);
		Assert.Equal(descriptor, await OverlayWire.ReceiveBinaryAsync(ws, cts.Token));
	}

	[Fact]
	public async Task Broadcast_ReachesEveryConnectedClient()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		byte[] descriptor = [0x01, 1, 0];
		byte[] payload = [0x02, 1, 0xAB, 0xCD];
		var port = OverlayWire.GetFreePort();
		using var server = new OverlayWebSocketServer(port, descriptor);
		server.Start();

		using var first = await OverlayWire.ConnectAsync(port, cts.Token);
		using var second = await OverlayWire.ConnectAsync(port, cts.Token);
		await OverlayWire.ReceiveBinaryAsync(first, cts.Token);
		await OverlayWire.ReceiveBinaryAsync(second, cts.Token);

		server.Broadcast(payload);

		Assert.Equal(payload, await OverlayWire.ReceiveBinaryAsync(first, cts.Token));
		Assert.Equal(payload, await OverlayWire.ReceiveBinaryAsync(second, cts.Token));
	}

	[Fact]
	public async Task Broadcast_DropsDeadClients_AndKeepsServingTheRest()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		byte[] descriptor = [0x01, 1, 0];
		byte[] payload = [0x02, 9];
		var port = OverlayWire.GetFreePort();
		using var server = new OverlayWebSocketServer(port, descriptor);
		server.Start();

		var doomed = await OverlayWire.ConnectAsync(port, cts.Token);
		using var survivor = await OverlayWire.ConnectAsync(port, cts.Token);
		await OverlayWire.ReceiveBinaryAsync(doomed, cts.Token);
		await OverlayWire.ReceiveBinaryAsync(survivor, cts.Token);
		Assert.Equal(2, server.ClientCount);

		// Kill one client hard; the dead socket surfaces as a failed send on
		// one of the following broadcasts and gets dropped.
		doomed.Abort();
		doomed.Dispose();
		while (server.ClientCount > 1)
		{
			cts.Token.ThrowIfCancellationRequested();
			server.Broadcast(payload);
			await Task.Delay(10, cts.Token);
		}

		// The survivor still gets frames.
		server.Broadcast(payload);
		Assert.Equal(payload, await OverlayWire.ReceiveBinaryAsync(survivor, cts.Token));
	}

	[Fact]
	public async Task StaticFiles_AreServedFromTheWebRoot()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		var webRoot = Directory.CreateTempSubdirectory("overlay-webroot-").FullName;
		try
		{
			File.WriteAllText(Path.Combine(webRoot, "joyviz.html"), "<html>root</html>");
			File.WriteAllText(Path.Combine(webRoot, "app.js"), "console.log(1)");

			var port = OverlayWire.GetFreePort();
			using var server = new OverlayWebSocketServer(port, [0x01, 1, 0], webRoot);
			server.Start();

			using var http = new HttpClient();
			var js = await http.GetAsync($"http://127.0.0.1:{port}/app.js", cts.Token);
			Assert.Equal(HttpStatusCode.OK, js.StatusCode);
			Assert.Equal("text/javascript", js.Content.Headers.ContentType?.MediaType);
			Assert.Equal("console.log(1)", await js.Content.ReadAsStringAsync(cts.Token));

			// "/" falls back to joyviz.html.
			var root = await http.GetAsync($"http://127.0.0.1:{port}/", cts.Token);
			Assert.Equal(HttpStatusCode.OK, root.StatusCode);
			Assert.Equal("<html>root</html>", await root.Content.ReadAsStringAsync(cts.Token));
		}
		finally
		{
			Directory.Delete(webRoot, recursive: true);
		}
	}

	[Fact]
	public async Task StaticFiles_MissingFileAndEscapedTraversal_Get404()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		var parent = Directory.CreateTempSubdirectory("overlay-traversal-").FullName;
		try
		{
			var webRoot = Directory.CreateDirectory(Path.Combine(parent, "webroot")).FullName;
			File.WriteAllText(Path.Combine(parent, "secret.txt"), "top secret");

			var port = OverlayWire.GetFreePort();
			using var server = new OverlayWebSocketServer(port, [0x01, 1, 0], webRoot);
			server.Start();

			using var http = new HttpClient();
			var missing = await http.GetAsync($"http://127.0.0.1:{port}/missing.html", cts.Token);
			Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

			// %2e%2e survives the client untouched; the server unescapes it and
			// must reject the resolved path outside the web root.
			var traversal = await http.GetAsync($"http://127.0.0.1:{port}/%2e%2e/secret.txt", cts.Token);
			Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	[Fact]
	public async Task PlainHttpWithoutWebRoot_Gets404()
	{
		using var cts = new CancellationTokenSource(OverlayWire.Timeout);
		var port = OverlayWire.GetFreePort();
		using var server = new OverlayWebSocketServer(port, [0x01, 1, 0]);
		server.Start();

		using var http = new HttpClient();
		var response = await http.GetAsync($"http://127.0.0.1:{port}/anything", cts.Token);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}
