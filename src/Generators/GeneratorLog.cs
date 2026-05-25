using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace SharpSticks.Generators;

/// File-based diagnostic log. Source generators run inside analyzer hosts where
/// stdout/stderr go nowhere visible, so we tail-write to a file under TMP/TEMP
/// to see when the pipeline re-runs versus hits the incremental cache. Every
/// failure mode here is swallowed — logging must never fail a build.
internal static class GeneratorLog
{
	private static readonly string LogPath = ResolveLogPath();
	private static readonly int ProcessId = GetProcessIdSafe();
	private static readonly object Gate = new();

	internal static void Log(string message)
	{
		try
		{
			var line = string.Concat(
				DateTime.UtcNow.ToString("HH:mm:ss.fff"),
				" pid=", ProcessId.ToString(),
				" tid=", Environment.CurrentManagedThreadId.ToString(),
				" ", message, "\n");
			lock (Gate)
			{
				File.AppendAllText(LogPath, line, Encoding.UTF8);
			}
		}
		catch
		{
			// Logging is best-effort.
		}
	}

	private static string ResolveLogPath()
	{
		try
		{
			var dir = Environment.GetEnvironmentVariable("TEMP")
				?? Environment.GetEnvironmentVariable("TMP")
				?? "/tmp";
			return Path.Combine(dir, "sharpsticks-gen.log");
		}
		catch
		{
			return "sharpsticks-gen.log";
		}
	}

	private static int GetProcessIdSafe()
	{
		try
		{
			return Process.GetCurrentProcess().Id;
		}
		catch
		{
			return 0;
		}
	}
}
