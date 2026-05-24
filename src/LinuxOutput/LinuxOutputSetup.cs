using System.Diagnostics;
using System.IO;

namespace SharpSticks.LinuxOutput;

/// One-time setup steps required before <see cref="LinuxOutputDeviceFactory"/> can be used
/// from a non-root process. Invoked via <c>setup uinput</c> on the host program's CLI.
public static class LinuxOutputSetup
{
	internal const string SubcommandName = "uinput";

	private const string UdevRulePath = "/etc/udev/rules.d/99-sharpsticks-uinput.rules";
	private const string UdevRuleBody =
		"# Installed by SharpSticks setup uinput\n" +
		"KERNEL==\"uinput\", GROUP=\"input\", MODE=\"0660\", TAG+=\"uaccess\"\n";

	/// Perform the OS-level setup (modprobe, udev rule, group membership) and then validate
	/// that each output device declared by the host's routes can actually be created via
	/// uinput. <paramref name="buttonRoutes"/>/<paramref name="axisRoutes"/> are filtered to
	/// the deviceIds we'll be creating; the implementation groups by OutputBinding.OutputDeviceId
	/// and runs one create/destroy cycle per distinct device.
	public static void Run(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			System.Console.Error.WriteLine("setup uinput only works on Linux.");
			Environment.Exit(2);
		}

		if (!IsRoot())
		{
			System.Console.Error.WriteLine("setup uinput must be run as root (e.g. via sudo / doas).");
			Environment.Exit(2);
		}

		System.Console.WriteLine("Setting up uinput...");

		LoadUinputModule();
		WriteUdevRule();
		ReloadUdev();
		AddInvokingUserToInputGroup();
		ValidateOutputDevices(buttonRoutes, axisRoutes, macroButtonNumbers);

		System.Console.WriteLine();
		System.Console.WriteLine("Setup complete.");
		System.Console.WriteLine("If you were just added to the 'input' group, log out and back in");
		System.Console.WriteLine("(or run 'newgrp input' in your current shell) for the membership");
		System.Console.WriteLine("to take effect. After that, the application can be run as your");
		System.Console.WriteLine("normal user — no sudo needed for input/output.");
	}

	private static bool IsRoot()
	{
		try
		{
			var (code, stdout, _) = RunCapturing("id", "-u");
			return code == 0 && stdout.Trim() == "0";
		}
		catch
		{
			return false;
		}
	}

	private static void LoadUinputModule()
	{
		var (code, _, _) = RunCapturing("modprobe", "uinput");
		if (code == 0)
		{
			System.Console.WriteLine("  ✓ uinput kernel module loaded");
			return;
		}

		if (File.Exists("/dev/uinput"))
		{
			System.Console.WriteLine("  ✓ /dev/uinput already present (kernel built-in or pre-loaded)");
			return;
		}

		System.Console.Error.WriteLine("  ✗ Could not load uinput module and /dev/uinput is missing.");
		Environment.Exit(3);
	}

	private static void WriteUdevRule()
	{
		if (File.Exists(UdevRulePath) && File.ReadAllText(UdevRulePath) == UdevRuleBody)
		{
			System.Console.WriteLine($"  ✓ udev rule already present at {UdevRulePath}");
			return;
		}

		Directory.CreateDirectory(Path.GetDirectoryName(UdevRulePath)!);
		File.WriteAllText(UdevRulePath, UdevRuleBody);
		System.Console.WriteLine($"  ✓ wrote udev rule {UdevRulePath}");
	}

	private static void ReloadUdev()
	{
		var reload = RunCapturing("udevadm", "control", "--reload-rules");
		var trigger = RunCapturing("udevadm", "trigger", "--subsystem-match=misc", "--attr-match=name=uinput");
		if (reload.code == 0 && trigger.code == 0)
		{
			System.Console.WriteLine("  ✓ udev rules reloaded and triggered");
		}
		else
		{
			System.Console.WriteLine("  ! udevadm reload/trigger reported non-zero — reboot may be required");
		}
	}

	private static void AddInvokingUserToInputGroup()
	{
		var user = Environment.GetEnvironmentVariable("SUDO_USER")
		           ?? Environment.GetEnvironmentVariable("DOAS_USER");
		if (string.IsNullOrEmpty(user))
		{
			System.Console.WriteLine("  ! SUDO_USER / DOAS_USER not set; skipping group membership.");
			System.Console.WriteLine("    Run 'usermod -aG input <user>' manually for the user that will use SharpSticks.");
			return;
		}

		var groups = RunCapturing("id", "-Gn", user);
		if (groups.code == 0 && groups.stdout.Split(' ', '\t', '\n').Any(g => g.Trim() == "input"))
		{
			System.Console.WriteLine($"  ✓ user '{user}' already in 'input' group");
			return;
		}

		var addResult = RunCapturing("usermod", "-aG", "input", user);
		if (addResult.code == 0)
		{
			System.Console.WriteLine($"  ✓ added user '{user}' to 'input' group");
		}
		else
		{
			System.Console.WriteLine($"  ! usermod -aG input {user} returned exit code {addResult.code}");
		}
	}

	private static void ValidateOutputDevices(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers)
	{
		var deviceIds = buttonRoutes.Select(static r => r.OutputBinding.OutputDeviceId)
			.Concat(axisRoutes.Select(static r => r.OutputBinding.OutputDeviceId))
			.Distinct()
			.OrderBy(static id => id)
			.ToArray();

		if (deviceIds.Length == 0)
		{
			System.Console.WriteLine("  i no output devices declared in routes; skipping per-device validation");
			return;
		}

		var factory = LinuxOutputDeviceFactory.Instance;
		foreach (var deviceId in deviceIds)
		{
			var deviceButtons = buttonRoutes.Where(r => r.OutputBinding.OutputDeviceId == deviceId).ToArray();
			var deviceAxes = axisRoutes.Where(r => r.OutputBinding.OutputDeviceId == deviceId).ToArray();
			var deviceMacroButtons = macroButtonNumbers
				.Where(_ => deviceButtons.Length > 0 || deviceAxes.Length > 0)
				.ToArray();

			try
			{
				using var probe = factory.EnumerateConnectedOutputDevices(
					new[] { new OutputDeviceRequest(deviceId, deviceButtons, deviceAxes, deviceMacroButtons) });
				System.Console.WriteLine(
					$"  ✓ output device {deviceId} created + destroyed " +
					$"({deviceAxes.Length} axes, {deviceButtons.Length} buttons)");
			}
			catch (Exception ex)
			{
				System.Console.Error.WriteLine($"  ✗ output device {deviceId}: {ex.Message}");
				Environment.Exit(4);
			}
		}
	}

	private static (int code, string stdout, string stderr) RunCapturing(string fileName, params string[] arguments)
	{
		var psi = new ProcessStartInfo
		{
			FileName = fileName,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var arg in arguments)
		{
			psi.ArgumentList.Add(arg);
		}

		try
		{
			using var process = Process.Start(psi);
			if (process is null)
			{
				return (127, "", "Process.Start returned null");
			}

			var stdout = process.StandardOutput.ReadToEnd();
			var stderr = process.StandardError.ReadToEnd();
			process.WaitForExit();
			return (process.ExitCode, stdout, stderr);
		}
		catch (Exception ex)
		{
			return (127, "", ex.Message);
		}
	}
}
