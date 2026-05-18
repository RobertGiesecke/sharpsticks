namespace SharpSticks.OutputAbstractions;

public static class RuntimeBuilder
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory? OutputDeviceFactory { get; init; }
		public ITimeSource? TimeSource { get; init; }
		public required ImmutableArray<JoystickDevice> ConnectedDevices { get; init; }
		public ImmutableArray<IBoundRoute> Routes { get; init; } = [];
	}

	extension(Runtime)
	{
		public static IOutputRuntimeContext Build(BuildOptions options)
		{
			var optionsOutputDeviceFactory = options.OutputDeviceFactory ??
			                                 throw new ArgumentNullException(nameof(options.OutputDeviceFactory));
			var timeSource = options.TimeSource ?? StopwatchTimeSource.Instance;

			using var connectedDevicesById = options.ConnectedDevices
				.ToPooledDictionary(device => device.DeviceId);
			using var referencedDeviceIds = new PooledSet<int>();
			using var buttonRoutes = options.Routes.OfType<ButtonRoute>().ToPooledList();
			using var axisRoutes = options.Routes.OfType<AxisRoute>().ToPooledList();
			using var macroRoutes = options.Routes.OfType<ButtonMacroRoute>().ToPooledList();
			using var axisToButtonRoutes = options.Routes.OfType<AxisToButtonRoute>().ToPooledList();
			using var claimedAxes = new PooledSet<(uint OutputDeviceId, Axis Axis)>();
			using var referencedOutputDeviceIds = new PooledSet<uint>();
			using var auxiliaryOutputButtons = new PooledSet<OutputButtonBinding>();

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

			foreach (var route in macroRoutes)
			{
				if (route.Binding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Source buttons are 1-based.");
				}

				referencedDeviceIds.Add(route.Binding.DeviceId);

				foreach (var action in route.OnPress)
				{
					action.FillOutputs(auxiliaryOutputButtons);
				}

				foreach (var action in route.OnRelease)
				{
					action.FillOutputs(auxiliaryOutputButtons);
				}
			}

			foreach (var route in axisToButtonRoutes)
			{
				if (route.OutputBinding.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				if (route.OutputBinding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Target buttons are 1-based.");
				}

				if (route.Max < route.Min)
				{
					throw new InvalidOperationException(
						$"AxisToButtonRoute: Max ({route.Max}) must be >= Min ({route.Min}).");
				}

				if (route.Mode == AxisZoneTriggerMode.Pulse && route.PulseDuration <= TimeSpan.Zero)
				{
					throw new InvalidOperationException(
						"AxisToButtonRoute.PulseDuration must be positive when Mode is Pulse.");
				}

				referencedDeviceIds.Add(route.Source.DeviceId);
				auxiliaryOutputButtons.Add(route.OutputBinding);
			}

			foreach (var output in auxiliaryOutputButtons)
			{
				if (output.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				if (output.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Target buttons are 1-based.");
				}

				referencedOutputDeviceIds.Add(output.OutputDeviceId);
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
						axisRoutes.Where(route => route.OutputBinding.OutputDeviceId == deviceId).ToArray(),
						// ReSharper disable once AccessToDisposedClosure
						auxiliaryOutputButtons.Where(b => b.OutputDeviceId == deviceId).Select(b => b.ButtonNumber).ToArray()))
					.ToImmutableArray();
				try
				{
					return new Runtime(
						options.Name,
						options.DebugLogger,
						devices,
						[..buttonRoutes],
						[..axisRoutes],
						[..macroRoutes],
						[..axisToButtonRoutes],
						[..auxiliaryOutputButtons],
						timeSource,
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
