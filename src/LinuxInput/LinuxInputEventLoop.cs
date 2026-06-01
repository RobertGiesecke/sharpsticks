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

		var events = EpollEvents.In | EpollEvents.Err | EpollEvents.Hup;
		var result = LinuxEpollEventLayout.IsPacked
			? Ctl<LinuxEpollEventPacked>(EpollCtlOp.Add, fd, events)
			: Ctl<LinuxEpollEventAligned>(EpollCtlOp.Add, fd, events);

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
			Ctl<LinuxEpollEventPacked>(EpollCtlOp.Del, fd, events: EpollEvents.None);
		}
		else
		{
			Ctl<LinuxEpollEventAligned>(EpollCtlOp.Del, fd, events: EpollEvents.None);
		}
	}

	/// Build the arch-correct epoll_event and issue an epoll_ctl op. For DEL the kernel ignores
	/// the event payload, so <paramref name="events"/> is just <see cref="EpollEvents.None"/> there.
	private static int Ctl<TEvent>(EpollCtlOp op, int fd, EpollEvents events)
		where TEvent : unmanaged, IEpollEvent<TEvent>
	{
		var ev = TEvent.Create(events, (ulong)fd);
		return TEvent.Ctl(_EpollFd, op, fd, ref ev);
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

			_EpollFd = LinuxLibc.EpollCreate1(OpenFlags.CloseOnExec);
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
			LoopBody<LinuxEpollEventPacked>(token);
		}
		else
		{
			LoopBody<LinuxEpollEventAligned>(token);
		}
	}

	private static void LoopBody<TEvent>(CancellationToken token)
		where TEvent : unmanaged, IEpollEvent<TEvent>
	{
		Span<TEvent> events = stackalloc TEvent[MaxEventsPerWait];
		while (!token.IsCancellationRequested)
		{
			var count = TEvent.Wait(
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