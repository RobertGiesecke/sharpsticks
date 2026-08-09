using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// Loopback plumbing for the overlay wire tests: ephemeral ports, WebSocket
/// clients (with a retry-connect for servers that are still starting on a
/// background task), and single-fragment binary receives.
/// </summary>
internal static class OverlayWire
{
	public static readonly TimeSpan Timeout = FromSeconds(10);

	/// <summary>Reserves an OS-assigned loopback port and releases it for reuse.</summary>
	public static int GetFreePort()
	{
		var probe = new TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		var port = ((IPEndPoint)probe.LocalEndpoint).Port;
		probe.Stop();
		return port;
	}

	public static async Task<ClientWebSocket> ConnectAsync(int port, CancellationToken cancellationToken)
	{
		while (true)
		{
			var ws = new ClientWebSocket();
			try
			{
				await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cancellationToken);
				return ws;
			}
			catch (WebSocketException)
			{
				// The server may still be starting on its background task.
				ws.Dispose();
				await Task.Delay(20, cancellationToken);
			}
		}
	}

	public static async Task<byte[]> ReceiveBinaryAsync(WebSocket ws, CancellationToken cancellationToken)
	{
		var buffer = new byte[64 * 1024];
		var result = await ws.ReceiveAsync(buffer, cancellationToken);
		Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
		Assert.True(result.EndOfMessage);
		return buffer.AsSpan(0, result.Count).ToArray();
	}
}
