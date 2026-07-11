using Collections.Pooled;

namespace SharpSticks.OverlayServer;

public static class ServeOptionsExtensions
{
	extension<TInputDevice>(ServeOptions<TInputDevice>) where TInputDevice : JoystickDevice
	{
		public static ServeOptions<TInputDevice>? BuildServeOptionsFromArgs(string[] args)
		{
			ushort? port = null;

			using var selectors = new PooledList<string>();
			string? webRoot = null;
			Func<TInputDevice, bool>? predicate = null;

			for (var i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "--port" when i + 1 < args.Length:
						if (!ushort.TryParse(args[++i], out var foundPort))
						{
							Console.Error.WriteLine("--port expects a positive number.");
							return null;
						}

						port = foundPort;

						break;
					case "--device" when i + 1 < args.Length:
						selectors.Add(args[++i]);
						break;
					case "--root" when i + 1 < args.Length:
						webRoot = args[++i];
						break;
					case "serve":
						break;
					default:
						if (args[i].StartsWith('-'))
						{
							Console.Error.WriteLine($"Unknown option '{args[i]}'.");
							return null;
						}

						break;
				}
			}

			if (selectors.Count > 0)
			{
				using var preparedSelectors = new PooledList<(int? deviceId, string namePart)>(selectors.Count);
				foreach (var selector in selectors)
				{
					if (selector is not { Length: > 0 })
					{
						continue;
					}

					int? usedId = int.TryParse(selector, out var id) ? id : null;
					preparedSelectors.Add((usedId, selector));
				}

				predicate = preparedSelectors.Count < 1
					? null
					: device =>
						// ReSharper disable once AccessToDisposedClosure
						preparedSelectors.Exists(tpl =>
							tpl.deviceId == device.DeviceId ||
							device.Name.Contains(tpl.namePart, StringComparison.OrdinalIgnoreCase));
			}

			return new()
			{
				Port = port,
				WebRoot = webRoot,
				DevicePredicate = predicate,
			};
		}
	}
}