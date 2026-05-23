using System.Collections.Concurrent;

namespace SharpSticks.LinuxInput;

/// Shared epoll-based event loop for all <see cref="LinuxInputJoystickDevice"/> instances.
/// Replaces a poll()-per-device-thread design: one thread blocks in <c>epoll_wait()</c>
/// over every registered device fd and signals the matching <see cref="AutoResetEvent"/>
/// when input is ready. O(1) wake-ups regardless of device count.
internal static class LinuxInputEventLoop
{
	private const int MaxEventsPerWait = 32;

	private static readonly Lock _LifecycleLock = new();
	private static readonly ConcurrentDictionary<int, AutoResetEvent> _Subscribers = new();
	private static int _EpollFd = -1;
	private static Thread? _LoopThread;
	private static CancellationTokenSource? _LoopCts;

	/// Register <paramref name="fd"/> in the shared epoll set and arrange for
	/// <paramref name="dataAvailable"/> to be signaled when the fd becomes readable.
	/// The first call lazily starts the background loop thread.
	public static void Register(int fd, AutoResetEvent dataAvailable)
	{
		EnsureLoopRunning();
		_Subscribers[fd] = dataAvailable;

		var events = LinuxEventCodes.EpollIn | LinuxEventCodes.EpollErr | LinuxEventCodes.EpollHup;
		int result;
		if (LinuxEpollEventLayout.IsPacked)
		{
			var ev = new LinuxEpollEventPacked { Events = events, Data = (ulong)fd };
			result = LinuxLibc.EpollCtlPacked(_EpollFd, LinuxEventCodes.EpollCtlAdd, fd, ref ev);
		}
		else
		{
			var ev = new LinuxEpollEventAligned { Events = events, Data = (ulong)fd };
			result = LinuxLibc.EpollCtlAligned(_EpollFd, LinuxEventCodes.EpollCtlAdd, fd, ref ev);
		}

		if (result < 0)
		{
			_Subscribers.TryRemove(fd, out _);
			throw new InvalidOperationException(
				$"epoll_ctl(ADD) failed for fd {fd}, errno {LinuxLibc.LastError}.");
		}
	}

	public static void Unregister(int fd)
	{
		_Subscribers.TryRemove(fd, out _);
		if (_EpollFd < 0)
		{
			return;
		}

		if (LinuxEpollEventLayout.IsPacked)
		{
			var ev = default(LinuxEpollEventPacked);
			LinuxLibc.EpollCtlPacked(_EpollFd, LinuxEventCodes.EpollCtlDel, fd, ref ev);
		}
		else
		{
			var ev = default(LinuxEpollEventAligned);
			LinuxLibc.EpollCtlAligned(_EpollFd, LinuxEventCodes.EpollCtlDel, fd, ref ev);
		}
	}

	private static void EnsureLoopRunning()
	{
		if (_LoopThread is not null)
		{
			return;
		}

		lock (_LifecycleLock)
		{
			if (_LoopThread is not null)
			{
				return;
			}

			_EpollFd = LinuxLibc.EpollCreate1(LinuxEventCodes.OCloseOnExec);
			if (_EpollFd < 0)
			{
				throw new InvalidOperationException(
					$"epoll_create1 failed, errno {LinuxLibc.LastError}.");
			}

			_LoopCts = new();
			_LoopThread = new(LoopBody)
			{
				IsBackground = true,
				Name = "SharpSticks LinuxInput epoll",
			};
			_LoopThread.Start(_LoopCts.Token);
		}
	}

	private static void LoopBody(object? rawToken)
	{
		var token = (CancellationToken)rawToken!;

		if (LinuxEpollEventLayout.IsPacked)
		{
			LoopBodyPacked(token);
		}
		else
		{
			LoopBodyAligned(token);
		}
	}

	private static void LoopBodyPacked(CancellationToken token)
	{
		Span<LinuxEpollEventPacked> events = stackalloc LinuxEpollEventPacked[MaxEventsPerWait];
		while (!token.IsCancellationRequested)
		{
			var count = LinuxLibc.EpollWaitPacked(
				_EpollFd, ref MemoryMarshal.GetReference(events), events.Length, 250);
			if (count <= 0)
			{
				continue;
			}

			for (var i = 0; i < count; i++)
			{
				SignalFd((int)events[i].Data);
			}
		}
	}

	private static void LoopBodyAligned(CancellationToken token)
	{
		Span<LinuxEpollEventAligned> events = stackalloc LinuxEpollEventAligned[MaxEventsPerWait];
		while (!token.IsCancellationRequested)
		{
			var count = LinuxLibc.EpollWaitAligned(
				_EpollFd, ref MemoryMarshal.GetReference(events), events.Length, 250);
			if (count <= 0)
			{
				continue;
			}

			for (var i = 0; i < count; i++)
			{
				SignalFd((int)events[i].Data);
			}
		}
	}

	private static void SignalFd(int fd)
	{
		if (_Subscribers.TryGetValue(fd, out var signal))
		{
			try
			{
				signal.Set();
			}
			catch (ObjectDisposedException)
			{
				// Subscriber disposed concurrently; safe to drop.
			}
		}
	}
}
