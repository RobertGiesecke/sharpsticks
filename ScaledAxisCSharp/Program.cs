using System.CommandLine;
using System.Text.Json;

namespace ScaledAxisCSharp;

internal static class Program
{
	private static int Main(string[] args)
	{
		if (!OperatingSystem.IsWindows())
		{
			Console.Error.WriteLine("This tool only runs on Windows because it depends on DirectInput and vJoy.");
			return 1;
		}

		try
		{
			return BuildRootCommand().Parse(args).Invoke();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			return 1;
		}
	}

	private static RootCommand BuildRootCommand() =>
		new("DirectInput to vJoy CLI.")
		{
			BuildListCommand(),
			BuildRunCommand(),
			BuildRunItbMinimalCommand(),
		};

	private static Command BuildListCommand()
	{
		var command = new Command("list", "List available DirectInput joystick devices.");
		command.SetAction(_ => ListDevices());
		return command;
	}

	private static Command BuildRunCommand()
	{
		var configOption = CreateConfigOption();
		var debugOption = CreateDebugOption();
		var debugIntervalOption = CreateDebugIntervalOption();

		var command = new Command("run", "Run the generic routing profile.")
		{
			configOption,
			debugOption,
			debugIntervalOption,
		};
		command.SetAction(parseResult =>
		{
			var configPath = parseResult.GetRequiredValue(configOption);
			var debugLogger = CreateDebugLogger(
				parseResult.GetValue(debugOption),
				parseResult.GetValue(debugIntervalOption));
			return Run(configPath, debugLogger);
		});

		return command;
	}

	private static Command BuildRunItbMinimalCommand()
	{
		var configOption = CreateConfigOption();
		var debugOption = CreateDebugOption();
		var debugIntervalOption = CreateDebugIntervalOption();

		var command = new Command("run-itb-minimal", "Run the ITB minimal profile.")
		{
			configOption,
			debugOption,
			debugIntervalOption,
		};
		command.SetAction(parseResult =>
		{
			var configPath = parseResult.GetRequiredValue(configOption);
			var debugLogger = CreateDebugLogger(
				parseResult.GetValue(debugOption),
				parseResult.GetValue(debugIntervalOption));
			return RunItbMinimal(configPath, debugLogger);
		});

		return command;
	}

	private static Option<string> CreateConfigOption()
	{
		return new Option<string>("--config", ["-c"])
		{
			Description = "Path to the JSON config file.",
			Required = true,
		};
	}

	private static Option<bool> CreateDebugOption() =>
		new("--debug")
		{
			Description = "Enable periodic debug logging to stderr.",
		};

	private static Option<int?> CreateDebugIntervalOption() =>
		new("--debug-interval-ms")
		{
			Description = "Debug log interval in milliseconds. Defaults to 250 when debug is enabled.",
		};

	private static DebugLogger? CreateDebugLogger(bool enabled, int? intervalMs)
	{
		if (!enabled && intervalMs is null)
		{
			return null;
		}

		if (intervalMs is < 1)
		{
			throw new InvalidOperationException("--debug-interval-ms must be an integer greater than 0.");
		}

		return new DebugLogger(intervalMs ?? 250);
	}

	private static int ListDevices()
	{
		var devices = JoystickDevice.EnumerateConnected();
		if (devices.Count == 0)
		{
			Console.WriteLine("No DirectInput joystick devices found.");
			return 0;
		}

		foreach (var device in devices)
		{
			Console.WriteLine($"Device {device.DeviceId}: {device.Name}");
			Console.WriteLine($"  Instance: {device.InstanceName}");
			Console.WriteLine(
				$"  Axes: {device.Caps.NumAxes}, Buttons: {device.Caps.NumButtons}, POVs: {device.Caps.NumPovs}");
		}

		return 0;
	}

	private static int Run(string configPath, DebugLogger? debugLogger)
	{
		configPath = Path.GetFullPath(configPath);
		var config = LoadConfig(configPath);
		var runtime = Runtime.Build(config);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cts.Cancel();
		};

		Console.WriteLine($"Running with config '{configPath}'. Press Ctrl+C to stop.");
		if (debugLogger is not null)
		{
			Console.WriteLine($"Debug logging enabled with interval {debugLogger.IntervalMs} ms.");
		}

		runtime.Run(cts.Token, debugLogger);
		return 0;
	}

	private static int RunItbMinimal(string configPath, DebugLogger? debugLogger)
	{
		configPath = Path.GetFullPath(configPath);
		var config = LoadItbMinimalConfig(configPath);
		var runtime = ItbMinimalRuntime.Build(config);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cts.Cancel();
		};

		Console.WriteLine($"Running ITB minimal profile with config '{configPath}'. Press Ctrl+C to stop.");
		if (debugLogger is not null)
		{
			Console.WriteLine($"Debug logging enabled with interval {debugLogger.IntervalMs} ms.");
		}

		runtime.Run(cts.Token, debugLogger);
		return 0;
	}

	private static AppConfig LoadConfig(string configPath)
	{
		if (!File.Exists(configPath))
		{
			throw new FileNotFoundException($"Config file not found: {configPath}");
		}

		var json = File.ReadAllText(configPath);
		return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig)
		       ?? throw new InvalidOperationException("Config file could not be deserialized.");
	}

	private static ItbMinimalConfig LoadItbMinimalConfig(string configPath)
	{
		if (!File.Exists(configPath))
		{
			throw new FileNotFoundException($"Config file not found: {configPath}");
		}

		var json = File.ReadAllText(configPath);
		return JsonSerializer.Deserialize(json, AppJsonContext.Default.ItbMinimalConfig)
		       ?? throw new InvalidOperationException("Config file could not be deserialized.");
	}
}
