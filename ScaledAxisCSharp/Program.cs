using System.CommandLine;
using System.Text.Json;

namespace ScaledAxisCSharp;

internal static class Program
{
	static void Abc()
	{
		using var connectedDevices = JoystickDevice.EnumerateConnected();

		var devices = new
		{
			left = connectedDevices.ResolveDevice("LEFT VPC Stick WarBRD"),
			right = connectedDevices.ResolveDevice("RIGHT VPC Stick WarBRD"),
		};

		var sourceAxes = new
		{
			X = devices.right.BindAxis(PhysicalAxis.X),
			Y = devices.right.BindAxis(PhysicalAxis.Y),
			Z = devices.right.BindAxis(PhysicalAxis.Z),
			modifier = devices.left.BindAxis(PhysicalAxis.Slider1),
		};

		var sourceButtons = new
		{
			rightPrimaryFireButton = devices.right.BindButton(1),
			leftPrimaryFireButton = devices.left.BindButton(1),
			leftAuxButton = devices.left.BindButton(11),
			secondaryFireButton = devices.right.BindButton(18),
		};

		var vJoyDevice = VJoyDevice.Open(
			1,
			[
				sourceButtons.rightPrimaryFireButton.RouteButton(1),
				sourceButtons.leftPrimaryFireButton.RouteButton(40),
				sourceButtons.leftAuxButton.RouteButton(79),
				sourceButtons.secondaryFireButton.RouteButton(22),
			],
			[
				sourceAxes.X.RouteAxis(VJoyAxis.X, 1.0, 0.0),
				sourceAxes.X.RouteAxis(VJoyAxis.Y, 1.0, 0.0),
				sourceAxes.X.RouteAxis(VJoyAxis.Z, 1.0, 0.0),
			],
			[]);
	}

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

	private static RootCommand BuildRootCommand()
	{
		return new RootCommand("DirectInput to vJoy CLI.")
		{
			BuildListCommand(),
			BuildWatchAxisCommand(),
			BuildRunCommand(),
			BuildRunItbMinimalCommand(),
		};
	}

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

	private static Command BuildWatchAxisCommand()
	{
		var deviceOption = new Option<string>("--device")
		{
			Description = "DirectInput device id or device name fragment.",
			Required = true,
		};
		var axesOption = new Option<string>("--axes")
		{
			Description = "Comma-separated axes to watch. Defaults to x,y,z,rx,ry,rz,slider1,slider2.",
		};
		var modeOption = new Option<string>("--mode")
		{
			Description = "Axis normalization mode: signed or unsigned. Defaults to signed.",
		};
		var intervalOption = new Option<int?>("--interval-ms")
		{
			Description = "Sampling interval in milliseconds. Defaults to 100.",
		};

		var command = new Command("watch-axis", "Watch raw DirectInput axis values without vJoy output.")
		{
			deviceOption,
			axesOption,
			modeOption,
			intervalOption,
		};

		command.SetAction(parseResult =>
		{
			var deviceSelector = parseResult.GetRequiredValue(deviceOption);
			var axisList = parseResult.GetValue(axesOption);
			var mode = parseResult.GetValue(modeOption);
			var intervalMs = parseResult.GetValue(intervalOption);
			return WatchAxis(deviceSelector, axisList, mode, intervalMs);
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
		return new Option<string>("--config", "-c")
		{
			Description = "Path to the JSON config file.",
			Required = true,
		};
	}

	private static Option<bool> CreateDebugOption()
	{
		return new Option<bool>("--debug")
		{
			Description = "Enable periodic debug logging to stderr.",
		};
	}

	private static Option<int?> CreateDebugIntervalOption()
	{
		return new Option<int?>("--debug-interval-ms")
		{
			Description = "Debug log interval in milliseconds. Defaults to 250 when debug is enabled.",
		};
	}

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
		using var devices = JoystickDevice.EnumerateConnected();
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

	private static int WatchAxis(string deviceSelector, string? axisList, string? modeValue, int? intervalMs)
	{
		if (intervalMs is < 1)
		{
			throw new InvalidOperationException("--interval-ms must be an integer greater than 0.");
		}

		var pollIntervalMs = intervalMs ?? 100;
		var mode = string.IsNullOrWhiteSpace(modeValue) ? AxisMode.Signed : AxisMode.Parse(modeValue);
		var axes = PhysicalAxis.ParseList(axisList);
		var device = JoystickDevice.ResolveDevice(deviceSelector);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			// ReSharper disable once AccessToDisposedClosure
			cts.Cancel();
		};

		Console.WriteLine($"Watching device {device.DeviceId}: {device.Name}");
		Console.WriteLine($"Mode: {mode.ToString().ToLowerInvariant()}, interval: {pollIntervalMs} ms");
		Console.WriteLine($"Axes: {string.Join(", ", axes.Select(FormatAxisName))}");

		while (!cts.IsCancellationRequested)
		{
			if (device.TryRead(out var state, out var error))
			{
				var line = string.Join(
					" | ",
					axes.Select(axis =>
					{
						var sample = device.ReadAxisDebugSample(state,
							new AxisBinding(device.DeviceId, axis, mode, false, 0.0));
						return
							$"{FormatAxisName(axis)} raw={sample.RawValue} range={sample.RangeMin}..{sample.RangeMax} decoder={FormatDecoderKind(sample.DecoderKind)} norm={sample.NormalizedValue:0.0000}";
					}));

				Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
			}
			else if (error is not null)
			{
				Console.Error.WriteLine(error);
			}

			if (cts.Token.WaitHandle.WaitOne(pollIntervalMs))
			{
				break;
			}
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
			// ReSharper disable once AccessToDisposedClosure
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
			// ReSharper disable once AccessToDisposedClosure
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

	private static string FormatAxisName(PhysicalAxis axis)
	{
		return axis switch
		{
			PhysicalAxis.X => "x",
			PhysicalAxis.Y => "y",
			PhysicalAxis.Z => "z",
			PhysicalAxis.Rx => "rx",
			PhysicalAxis.Ry => "ry",
			PhysicalAxis.Rz => "rz",
			PhysicalAxis.Slider1 => "slider1",
			PhysicalAxis.Slider2 => "slider2",
			_ => axis.ToString(),
		};
	}

	private static string FormatDecoderKind(AxisDecoderKind decoderKind)
	{
		return decoderKind switch
		{
			AxisDecoderKind.Unsigned => "unsigned",
			AxisDecoderKind.NativeSigned => "native-signed",
			AxisDecoderKind.UnsignedCentered => "unsigned-centered",
			_ => "unknown",
		};
	}
}