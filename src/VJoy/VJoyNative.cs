using System.Reflection;
using System.Runtime.InteropServices;
using Collections.Pooled;

namespace SharpSticks.VJoy;

public static partial class VJoyNative
{
	static VJoyNative()
	{
		NativeLibrary.SetDllImportResolver(typeof(VJoyNative).Assembly, ResolveLibrary);
	}

	public static void EnsureLoaded()
	{
	}

	private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (!libraryName.Equals("vJoyInterface.dll", StringComparison.OrdinalIgnoreCase) &&
		    !libraryName.Equals("vJoyInterface", StringComparison.OrdinalIgnoreCase))
		{
			return IntPtr.Zero;
		}

		using var candidatePaths = GetCandidatePaths();

		foreach (var candidate in candidatePaths)
		{
			if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
			{
				continue;
			}

			if (NativeLibrary.TryLoad(candidate, out var handle))
			{
				return handle;
			}
		}

		return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var fallbackHandle)
			? fallbackHandle
			: IntPtr.Zero;
	}

	private static PooledList<string?> GetCandidatePaths()
	{
		var result = new PooledList<string?>();
		try
		{
			result.Add(Environment.GetEnvironmentVariable("VJOY_DLL_PATH"));
			result.Add(Path.Combine(AppContext.BaseDirectory, "vJoyInterface.dll"));

			var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

			if (Environment.Is64BitProcess)
			{
				result.Add(Path.Combine(programFiles, "vJoy", "x64", "vJoyInterface.dll"));
				result.Add(Path.Combine(programFilesX86, "vJoy", "x64", "vJoyInterface.dll"));
			}
			else
			{
				result.Add(Path.Combine(programFilesX86, "vJoy", "x86", "vJoyInterface.dll"));
				result.Add(Path.Combine(programFiles, "vJoy", "x86", "vJoyInterface.dll"));
			}

			return result;
		}
		catch
		{
			result.Dispose();
			throw;
		}
	}

	// vJoy's export is lowercase `vJoyEnabled`; the others match their PascalCase method names.
	// The SetDllImportResolver above still applies — LibraryImport stubs honour it.
	[LibraryImport("vJoyInterface.dll", EntryPoint = "vJoyEnabled")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool VJoyEnabled();

	[LibraryImport("vJoyInterface.dll")]
	public static partial VjdStatus GetVJDStatus(uint deviceId);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool AcquireVJD(uint deviceId);

	[LibraryImport("vJoyInterface.dll")]
	public static partial void RelinquishVJD(uint deviceId);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool ResetVJD(uint deviceId);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool GetVJDAxisExist(uint deviceId, uint axisUsage);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool GetVJDAxisMin(uint deviceId, uint axisUsage, ref int min);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool GetVJDAxisMax(uint deviceId, uint axisUsage, ref int max);

	[LibraryImport("vJoyInterface.dll")]
	public static partial int GetVJDButtonNumber(uint deviceId);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool SetAxis(int value, uint deviceId, uint axisUsage);

	[LibraryImport("vJoyInterface.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool SetBtn([MarshalAs(UnmanagedType.Bool)] bool pressed, uint deviceId, uint buttonNumber);
}