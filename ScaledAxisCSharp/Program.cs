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
			switch (args.Length)
			{
				case 0:
					PrintUsage();
					return 1;
				default:
					return args[0].ToLowerInvariant() switch
					{
						"list" => ListDevices(),
						"run" => Run(args.Skip(1).ToArray()),
						"run-itb-minimal" => RunItbMinimal(args.Skip(1).ToArray()),
						_ => FailWithUsage($"Unknown command '{args[0]}'."),
					};
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			return 1;
		}
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
			Console.WriteLine($"  Axes: {device.Caps.NumAxes}, Buttons: {device.Caps.NumButtons}, POVs: {device.Caps.NumPovs}");
		}

		return 0;
	}

	private static int Run(string[] args)
	{
		var configPath = ParseConfigPath(args);
		var config = LoadConfig(configPath);
		var runtime = Runtime.Build(config);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cts.Cancel();
		};

		Console.WriteLine($"Running with config '{configPath}'. Press Ctrl+C to stop.");
		runtime.Run(cts.Token);
		return 0;
	}

	private static int RunItbMinimal(string[] args)
	{
		var configPath = ParseConfigPath(args);
		var config = LoadItbMinimalConfig(configPath);
		var runtime = ItbMinimalRuntime.Build(config);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cts.Cancel();
		};

		Console.WriteLine($"Running ITB minimal profile with config '{configPath}'. Press Ctrl+C to stop.");
		runtime.Run(cts.Token);
		return 0;
	}

	private static string ParseConfigPath(string[] args)
	{
		for (var index = 0; index < args.Length; index++)
		{
			var current = args[index];
			if (string.Equals(current, "--config", StringComparison.OrdinalIgnoreCase) ||
			    string.Equals(current, "-c", StringComparison.OrdinalIgnoreCase))
			{
				if (index + 1 >= args.Length)
				{
					throw new InvalidOperationException("Missing value for --config.");
				}

				return Path.GetFullPath(args[index + 1]);
			}
		}

		throw new InvalidOperationException("Missing required --config option.");
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

	private static int FailWithUsage(string message)
	{
		Console.Error.WriteLine(message);
		PrintUsage();
		return 1;
	}

	private static void PrintUsage()
	{
		Console.WriteLine("Usage:");
		Console.WriteLine("  ScaledAxisCSharp list");
		Console.WriteLine("  ScaledAxisCSharp run --config <path-to-config.json>");
		Console.WriteLine("  ScaledAxisCSharp run-itb-minimal --config <path-to-itb-config.json>");
	}
}
