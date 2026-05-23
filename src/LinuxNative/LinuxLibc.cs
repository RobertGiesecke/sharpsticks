using System.Runtime.InteropServices;

namespace SharpSticks.LinuxNative;

/// libc P/Invokes shared between SharpSticks.LinuxInput and SharpSticks.LinuxOutput.
/// Uses <see cref="LibraryImportAttribute"/> for source-generated marshalling → AOT-safe.
public static partial class LinuxLibc
{
	private const string Libc = "libc";

	[LibraryImport(Libc, EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
	public static partial int Open(string path, int flags);

	[LibraryImport(Libc, EntryPoint = "close", SetLastError = true)]
	public static partial int Close(int fd);

	[LibraryImport(Libc, EntryPoint = "read", SetLastError = true)]
	public static partial nint Read(int fd, ref byte buffer, nuint count);

	[LibraryImport(Libc, EntryPoint = "write", SetLastError = true)]
	public static partial nint Write(int fd, ref byte buffer, nuint count);

	[LibraryImport(Libc, EntryPoint = "poll", SetLastError = true)]
	public static partial int Poll(ref LinuxPollFd fds, nuint nfds, int timeoutMs);

	// ioctl variants — one per third-arg shape we use, because P/Invoke needs concrete signatures
	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlNoArg(int fd, nuint request);

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlInt(int fd, nuint request, int value);

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlBuffer(int fd, nuint request, ref byte buffer);

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlInputId(int fd, nuint request, ref LinuxInputId value);

	[LibraryImport(Libc, EntryPoint = "ioctl", SetLastError = true)]
	public static partial int IoctlAbsInfo(int fd, nuint request, ref LinuxAbsInfo value);

	// epoll syscalls — used by LinuxInput's shared event loop
	[LibraryImport(Libc, EntryPoint = "epoll_create1", SetLastError = true)]
	public static partial int EpollCreate1(int flags);

	[LibraryImport(Libc, EntryPoint = "epoll_ctl", SetLastError = true)]
	public static partial int EpollCtlPacked(int epfd, int op, int fd, ref LinuxEpollEventPacked ev);

	[LibraryImport(Libc, EntryPoint = "epoll_ctl", SetLastError = true)]
	public static partial int EpollCtlAligned(int epfd, int op, int fd, ref LinuxEpollEventAligned ev);

	[LibraryImport(Libc, EntryPoint = "epoll_wait", SetLastError = true)]
	public static partial int EpollWaitPacked(int epfd, ref LinuxEpollEventPacked events, int maxEvents, int timeoutMs);

	[LibraryImport(Libc, EntryPoint = "epoll_wait", SetLastError = true)]
	public static partial int EpollWaitAligned(int epfd, ref LinuxEpollEventAligned events, int maxEvents, int timeoutMs);

	public static int LastError => Marshal.GetLastPInvokeError();
}

/// <c>struct epoll_event</c>. Layout differs by arch:
/// <list type="bullet">
///   <item>x86_64: <c>__attribute__((packed))</c> — 12 bytes, data at offset 4</item>
///   <item>aarch64 and everything else: natural alignment — 16 bytes, data at offset 8</item>
/// </list>
/// Both layouts are declared explicitly; <see cref="LinuxEpollEventLayout.IsPacked"/> picks
/// the right one at runtime.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LinuxEpollEventPacked
{
	public uint Events;
	public ulong Data;
}

[StructLayout(LayoutKind.Sequential)]
public struct LinuxEpollEventAligned
{
	public uint Events;
	private uint _Padding;
	public ulong Data;
}

public static class LinuxEpollEventLayout
{
	public static bool IsPacked { get; } =
		RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
		RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
}
