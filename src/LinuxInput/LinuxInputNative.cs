using System.Runtime.InteropServices;

namespace SharpSticks.LinuxInput;

/// libc P/Invokes used by the evdev backend. Source-generated marshalling
/// (LibraryImport) so this is NativeAOT-compatible.
internal static partial class LinuxInputNative
{
	private const string Libc = "libc";

	[LibraryImport(Libc, EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
	public static partial int Open(string path, int flags);

	[LibraryImport(Libc, EntryPoint = "close")]
	public static partial int Close(int fd);

	[LibraryImport(Libc, EntryPoint = "read")]
	public static partial nint Read(int fd, ref byte buffer, nuint count);

	[LibraryImport(Libc, EntryPoint = "poll")]
	public static partial int Poll(ref LinuxPollFd fds, nuint nfds, int timeoutMs);

	[LibraryImport(Libc, EntryPoint = "ioctl")]
	public static partial int IoctlBuffer(int fd, nuint request, ref byte buffer);

	[LibraryImport(Libc, EntryPoint = "ioctl")]
	public static partial int IoctlInputId(int fd, nuint request, ref LinuxInputId value);

	[LibraryImport(Libc, EntryPoint = "ioctl")]
	public static partial int IoctlAbsInfo(int fd, nuint request, ref LinuxAbsInfo value);

	public static int LastError => Marshal.GetLastPInvokeError();
}
