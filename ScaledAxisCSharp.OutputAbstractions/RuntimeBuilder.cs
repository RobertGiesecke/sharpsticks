namespace ScaledAxisCSharp.OutputAbstractions;

public static class RuntimeBuilder
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory? OutputDeviceFactory { get; init; }
		public required ImmutableArray<JoystickDevice> ConnectedDevices { get; init; }
		public ImmutableArray<IRoute> Routes { get; init; } = [];
	}

	extension(Runtime)
	{
		public static IOutputRuntimeContext Build(BuildOptions options)
		{
			var optionsOutputDeviceFactory = options.OutputDeviceFactory ??
			                                 throw new ArgumentNullException(nameof(options.OutputDeviceFactory));

			using var connectedDevicesById = options.ConnectedDevices
				.ToPooledDictionary(device => device.DeviceId);
			using var referencedDeviceIds = new PooledSet<int>();
			using var buttonRoutes = options.Routes.OfType<ButtonRoute>().ToPooledList();
			using var axisRoutes = options.Routes.OfType<AxisRoute>().ToPooledList();
			using var claimedAxes = new PooledSet<(uint OutputDeviceId, Axis Axis)>();
			using var referencedOutputDeviceIds = new PooledSet<uint>();

			foreach (var mapping in buttonRoutes)
			{
				if (mapping.Binding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Source buttons are 1-based.");
				}

				if (mapping.OutputBinding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Target buttons are 1-based.");
				}

				if (mapping.OutputBinding.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				referencedDeviceIds.Add(mapping.Binding.DeviceId);
				referencedOutputDeviceIds.Add(mapping.OutputBinding.OutputDeviceId);
			}

			foreach (var mapping in axisRoutes)
			{
				if (mapping.OutputBinding.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				if (!claimedAxes.Add((mapping.OutputBinding.OutputDeviceId, mapping.OutputBinding.Axis)))
				{
					throw new InvalidOperationException(
						$"Target axis '{mapping.OutputBinding.Axis}' on Output device {mapping.OutputBinding.OutputDeviceId} is mapped more than once.");
				}

				referencedDeviceIds.Add(mapping.Source.DeviceId);
				if (mapping.Modifier is { } m)
				{
					m.FillDevices(referencedDeviceIds);
				}

				referencedOutputDeviceIds.Add(mapping.OutputBinding.OutputDeviceId);
			}


			var devices = new PooledDictionary<int, JoystickDevice>();
			try
			{
				foreach (var device in options.ConnectedDevices)
				{
					if (!referencedDeviceIds.Contains(device.DeviceId))
					{
						device.Dispose();
					}
				}

				foreach (var deviceId in referencedDeviceIds)
				{
					if (!connectedDevicesById.TryGetValue(deviceId, out var device))
					{
						throw new InvalidOperationException(
							$"Configured joystick {deviceId} is not available via DirectInput.");
					}

					devices.Add(deviceId, device);
				}

				var outputDevices = referencedOutputDeviceIds
					.OrderBy(deviceId => deviceId)
					.Select<uint, OutputDevice>(deviceId => optionsOutputDeviceFactory.Open(
						deviceId,
						// ReSharper disable once AccessToDisposedClosure
						buttonRoutes.Where(route => route.OutputBinding.OutputDeviceId == deviceId).ToArray(),
						// ReSharper disable once AccessToDisposedClosure
						axisRoutes.Where(route => route.OutputBinding.OutputDeviceId == deviceId).ToArray()))
					.ToImmutableArray();
				try
				{
					return new Runtime(
						options.Name,
						options.DebugLogger,
						devices,
						[..buttonRoutes],
						[..axisRoutes],
						outputDevices);
				}
				catch
				{
					foreach (var outputDevice in outputDevices)
					{
						outputDevice.Dispose();
					}

					throw;
				}
			}
			catch
			{
				Runtime.DisposeDevices(devices.Values);
				foreach (var device in options.ConnectedDevices)
				{
					if (!devices.TryGetValue(device.DeviceId, out var selected) || !ReferenceEquals(selected, device))
					{
						device.Dispose();
					}
				}

				devices.Dispose();
				throw;
			}
		}
	}
}