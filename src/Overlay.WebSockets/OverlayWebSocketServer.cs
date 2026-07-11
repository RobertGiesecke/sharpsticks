using System.Buffers;
using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// Minimal, dependency-free WebSocket server over <see cref="TcpListener"/> — AOT-clean and
/// small enough to control fully. It only ever sends server-to-client binary frames (the
/// overlay never sends application data), so inbound frames are ignored; a client that has
/// gone away is detected when a send throws and is dropped. Sends are synchronous on the
/// caller's thread (the read loop) so there is no per-frame Task/await allocation.
/// </summary>
internal sealed class OverlayWebSocketServer : IDisposable
{
	private static readonly byte[] MagicBytes = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"u8.ToArray();

	private readonly TcpListener _Listener;
	private readonly byte[] _Descriptor;
	private readonly string? _WebRoot;
	// A SemaphoreSlim rather than a Lock so the async broadcast can hold it across an await
	// (a Lock/Monitor can't be held over await). Taken synchronously (Wait) on the accept
	// thread and the sync broadcast, asynchronously (WaitAsync) by BroadcastAsync.
	private readonly SemaphoreSlim _Sync = new(1, 1);
	private readonly List<TcpClient> _Clients = [];
	private readonly byte[] _Header = new byte[4];
	private Thread? _AcceptThread;
	private volatile bool _Running;

	// A stuck client must not block the accept thread's handshake read or a broadcast's
	// writes (the write happens while _Sync is held). Socket-level timeouts turn "blocked
	// forever" into an IOException, which the callers already treat as "drop this client".
	// These govern synchronous socket ops; an async send path would need a CancellationToken.
	private const int HandshakeReadTimeoutMs = 5000;
	private const int ClientSendTimeoutMs = 1000;

	/// <summary>
	/// Represents a minimal WebSocket server implementation that uses <see cref="TcpListener"/> to serve binary frames to connected clients.
	/// It supports serving static files when a root directory is provided, otherwise only WebSocket upgrades are handled.
	/// </summary>
	/// <param name="port">
	/// The port on which the WebSocket server will listen for incoming connections.
	/// </param>
	/// <param name="descriptor">
	/// A binary descriptor that is sent to clients during the WebSocket communication.
	/// </param>
	/// <param name="webRoot">
	/// The root directory for serving static files over plain HTTP GET requests. When null, static file serving is disabled, and only WebSocket upgrades are handled.
	/// </param>
	public OverlayWebSocketServer(int port, byte[] descriptor, string? webRoot = null)
	{
		_Descriptor = descriptor;
		_WebRoot = webRoot is null ? null : Path.GetFullPath(webRoot);
		_Listener = new(IPAddress.Loopback, port);
	}

	public int ClientCount
	{
		get
		{
			_Sync.Wait();
			try
			{
				return _Clients.Count;
			}
			finally
			{
				_Sync.Release();
			}
		}
	}

	public void Start()
	{
		_Running = true;
		_Listener.Start();
		_AcceptThread = new(AcceptLoop) { IsBackground = true, Name = "overlay-ws-accept" };
		_AcceptThread.Start();
	}

	private void AcceptLoop()
	{
		while (_Running)
		{
			TcpClient client;
			try
			{
				client = _Listener.AcceptTcpClient();
			}
			catch (SocketException)
			{
				break; // listener stopped
			}
			catch (ObjectDisposedException)
			{
				break;
			}

			try
			{
				HandleConnection(client);
			}
			catch
			{
				client.Dispose();
			}
		}
	}

	// Reads one request and dispatches it: a WebSocket upgrade becomes a live client,
	// anything else is served as a static file (when a web root is configured). The
	// connection is kept open only for an accepted WebSocket; HTTP replies are one-shot.
	private void HandleConnection(TcpClient client)
	{
		client.NoDelay = true;
		client.ReceiveTimeout = HandshakeReadTimeoutMs;
		client.SendTimeout = ClientSendTimeoutMs;
		var stream = client.GetStream();

		var request = ReadHttpHeaders(stream);
		if (request is { Length: > 0 })
		{
			var req = request.AsSpan();
			if (TryFindWebSocketKey(req, out var key))
			{
				CompleteHandshake(stream, key);
				// Send the descriptor first so the client can interpret state frames. Done
				// under the lock so the shared header buffer isn't shared with Broadcast.
				_Sync.Wait();
				try
				{
					SendFrame(stream, _Descriptor);
					_Clients.Add(client);
				}
				finally
				{
					_Sync.Release();
				}

				return; // client is now owned by _Clients
			}

			if (_WebRoot is not null)
			{
				ServeStaticFile(stream, req);
			}
			else
			{
				WriteHttpStatus(stream, "404 Not Found"u8);
			}
		}

		client.Dispose();
	}

	private static void CompleteHandshake(NetworkStream stream, ReadOnlySpan<byte> key)
	{
		// accept = base64(SHA1(key + magic)), assembled in stack buffers (no string/array).
		Span<byte> keyPlusMagic = stackalloc byte[64];
		key.CopyTo(keyPlusMagic);
		MagicBytes.CopyTo(keyPlusMagic[key.Length..]);
		Span<byte> hash = stackalloc byte[20];
		SHA1.HashData(keyPlusMagic[..(key.Length + MagicBytes.Length)], hash);

		Span<byte> response = stackalloc byte[160];
		var pos = 0;
		var responseHeader =
			"HTTP/1.1 101 Switching Protocols\r\n"u8 +
			"Upgrade: websocket\r\n"u8 +
			"Connection: Upgrade\r\n"u8 +
			"Sec-WebSocket-Accept: "u8;
		pos += Append(response[pos..], responseHeader);
		Base64.EncodeToUtf8(hash, response[pos..], out _, out var acceptWritten);
		pos += acceptWritten;
		pos += Append(response[pos..], "\r\n\r\n"u8);

		stream.Write(response[..pos]);
	}

	// ---- Static file serving (page-load path; ordinary allocations are fine here) -------
	private void ServeStaticFile(NetworkStream stream, ReadOnlySpan<byte> request)
	{
		if (!TryGetRequestTarget(request, out var target))
		{
			WriteHttpStatus(stream, "400 Bad Request"u8);
			return;
		}

		var relative = target == "/" ? "joyviz.html" : Uri.UnescapeDataString(target.TrimStart('/'));
		var full = Path.GetFullPath(Path.Combine(_WebRoot!, relative));
		// Reject path traversal outside the web root.
		if (!full.StartsWith(_WebRoot!, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
		{
			WriteHttpStatus(stream, "404 Not Found"u8);
			return;
		}

		var body = File.ReadAllBytes(full);
		var header = "HTTP/1.1 200 OK\r\nContent-Type: " + ContentType(full) +
		             "\r\nContent-Length: " + body.Length +
		             "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
		stream.Write(Encoding.ASCII.GetBytes(header));
		stream.Write(body);
	}

	private static bool TryGetRequestTarget(ReadOnlySpan<byte> request, out string target)
	{
		target = "";
		var lineEnd = request.IndexOf((byte)'\n');
		var line = lineEnd < 0 ? request : request[..lineEnd];
		var firstSpace = line.IndexOf((byte)' ');
		if (firstSpace < 0)
		{
			return false;
		}

		var afterMethod = line[(firstSpace + 1)..];
		var secondSpace = afterMethod.IndexOf((byte)' ');
		var raw = secondSpace < 0 ? afterMethod : afterMethod[..secondSpace];
		var query = raw.IndexOf((byte)'?');
		if (query >= 0)
		{
			raw = raw[..query];
		}

		if (raw.Length == 0 || raw[0] != (byte)'/')
		{
			return false;
		}

		target = Encoding.ASCII.GetString(raw);
		return true;
	}

	private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
	{
		".html" or ".htm" => "text/html; charset=utf-8",
		".js" => "text/javascript",
		".css" => "text/css",
		".json" => "application/json",
		".svg" => "image/svg+xml",
		".ico" => "image/x-icon",
		".png" => "image/png",
		".woff2" => "font/woff2",
		_ => "application/octet-stream",
	};

	private static void WriteHttpStatus(NetworkStream stream, ReadOnlySpan<byte> status)
	{
		Span<byte> response = stackalloc byte[64];
		var pos = 0;
		pos += Append(response[pos..], "HTTP/1.1 "u8);
		status.CopyTo(response[pos..]);
		pos += status.Length;
		pos += Append(response[pos..], "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8);
		stream.Write(response[..pos]);
	}

	private static int Append(Span<byte> destination, ReadOnlySpan<byte> source)
	{
		source.CopyTo(destination);
		return source.Length;
	}

	// Reads until the CRLFCRLF end-of-headers and returns exactly those bytes, or null.
	// Grows to accommodate long request lines: the overlay's "direct link" encodes the whole
	// config into the query string, so a GET can be tens of KB — a fixed small buffer would
	// silently drop it and the browser would show a page-load error.
	private static byte[]? ReadHttpHeaders(NetworkStream stream)
	{
		var httpDelimiter = "\r\n\r\n"u8;
		var buffer = new byte[8192];
		var total = 0;
		while (true)
		{
			if (total == buffer.Length)
			{
				if (buffer.Length >= 512 * 1024)
				{
					return null; // guard against an unbounded request
				}

				Array.Resize(ref buffer, buffer.Length * 2);
			}

			var read = stream.Read(buffer, total, buffer.Length - total);
			if (read <= 0)
			{
				return null;
			}

			total += read;
			if (total >= 4 && buffer.AsSpan(total - 4, 4).SequenceEqual(httpDelimiter))
			{
				Array.Resize(ref buffer, total);
				return buffer;
			}
		}
	}

	private static bool TryFindWebSocketKey(ReadOnlySpan<byte> request, out ReadOnlySpan<byte> key)
	{
		var header = "sec-websocket-key:"u8;
		var offset = 0;
		while (offset < request.Length)
		{
			var newline = request[offset..].IndexOf((byte)'\n');
			var line = newline < 0 ? request[offset..] : request.Slice(offset, newline);
			if (line.Length > 0 && line[^1] == (byte)'\r')
			{
				line = line[..^1];
			}

			if (StartsWithIgnoreAsciiCase(line, header))
			{
				key = TrimAsciiSpace(line[header.Length..]);
				return key.Length > 0;
			}

			if (newline < 0)
			{
				break;
			}

			offset += newline + 1;
		}

		key = default;
		return false;
	}

	private static bool StartsWithIgnoreAsciiCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lowerPrefix)
	{
		if (value.Length < lowerPrefix.Length)
		{
			return false;
		}

		for (var i = 0; i < lowerPrefix.Length; i++)
		{
			var c = value[i];
			if (c >= 'A' && c <= 'Z')
			{
				c += 32; // to lower
			}

			if (c != lowerPrefix[i])
			{
				return false;
			}
		}

		return true;
	}

	private static ReadOnlySpan<byte> TrimAsciiSpace(ReadOnlySpan<byte> value)
	{
		var start = 0;
		var end = value.Length;
		while (start < end && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
		{
			start++;
		}

		while (end > start && (value[end - 1] == (byte)' ' || value[end - 1] == (byte)'\t'))
		{
			end--;
		}

		return value[start..end];
	}

	/// <summary>Broadcasts one binary frame to every connected client; drops any that fail.</summary>
	public void Broadcast(ReadOnlySpan<byte> payload)
	{
		_Sync.Wait();
		try
		{
			for (var i = _Clients.Count - 1; i >= 0; i--)
			{
				var client = _Clients[i];
				try
				{
					SendFrame(client.GetStream(), payload);
				}
				catch
				{
					_Clients.RemoveAt(i);
					client.Dispose();
				}
			}
		}
		finally
		{
			_Sync.Release();
		}
	}

	/// <summary>Broadcasts one binary frame to every connected client; drops any that fail.</summary>
	public async Task BroadcastAsync(ReadOnlyMemory<byte> payload)
	{
		await _Sync.WaitAsync();
		try
		{
			for (var i = _Clients.Count - 1; i >= 0; i--)
			{
				var client = _Clients[i];
				try
				{
					await SendFrameAsync(client.GetStream(), payload);
				}
				catch
				{
					_Clients.RemoveAt(i);
					client.Dispose();
				}
			}
		}
		finally
		{
			_Sync.Release();
		}
	}

	private void SendFrame(NetworkStream stream, ReadOnlySpan<byte> payload)
	{
		Span<byte> headerSpan = _Header;

		var headerLen = BuildHeaderForPayload(payload, headerSpan);

		stream.Write(_Header, 0, headerLen);
		stream.Write(payload);
	}

	private async Task SendFrameAsync(NetworkStream stream, ReadOnlyMemory<byte> payload)
	{
		using var headerOwner = MemoryPool<byte>.Shared.Rent(_Header.Length);
		var headerSpan = headerOwner.Memory.Span;

		var headerLen = BuildHeaderForPayload(payload.Span, headerSpan);

		await stream.WriteAsync(headerOwner.Memory[..headerLen]);
		await stream.WriteAsync(payload);
	}

	private static int BuildHeaderForPayload(ReadOnlySpan<byte> payload, Span<byte> headerSpan)
	{
		// FIN + binary opcode (0x2); unmasked (server -> client).
		headerSpan[0] = 0x82;
		if (payload.Length <= 125)
		{
			headerSpan[1] = (byte)payload.Length;
			return 2;
		}

		headerSpan[1] = 126;
		headerSpan[2] = (byte)(payload.Length >> 8);
		headerSpan[3] = (byte)(payload.Length & 0xFF);
		return 4;
	}

	public void Dispose()
	{
		_Running = false;
		try
		{
			_Listener.Stop();
		}
		catch
		{
			// ignore
		}

		_Sync.Wait();
		try
		{
			foreach (var client in _Clients)
			{
				client.Dispose();
			}

			_Clients.Clear();
		}
		finally
		{
			_Sync.Release();
		}

		_Sync.Dispose();
	}
}