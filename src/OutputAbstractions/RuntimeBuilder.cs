namespace SharpSticks.OutputAbstractions;

public static class RuntimeBuilder
{
	public readonly record struct BuildOptions<TInputDevice, TOutputDevice>()
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public required string Name { get; init; }
		public DebugLogger? DebugLogger { get; init; }
		public IOutputDeviceFactory<TOutputDevice>? OutputDeviceFactory { get; init; }
		public ITimeSource? TimeSource { get; init; }

		/// <summary>
		/// OS keyboard/mouse event sink for key/mouse macro actions. Optional —
		/// leave null for profiles that don't synthesize input. A profile that
		/// uses key/mouse macro actions without one fails fast at build.
		/// </summary>
		public IInputSynthesizer? InputSynthesizer { get; init; }
		public required ImmutableArray<TInputDevice> ConnectedDevices { get; init; }
		public ImmutableArray<IRoute> Routes { get; init; } = [];
	}

	extension<TInputDevice, TOutputDevice>(Runtime<TInputDevice, TOutputDevice>)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public static IOutputRuntimeContext<TInputDevice, TOutputDevice> Build(
			BuildOptions<TInputDevice, TOutputDevice> options)
		{
			var optionsOutputDeviceFactory = options.OutputDeviceFactory ??
			                                 throw new ArgumentNullException(nameof(options.OutputDeviceFactory));
			var timeSource = options.TimeSource ?? StopwatchTimeSource.Instance;

			using var connectedDevicesById = options.ConnectedDevices
				.ToPooledDictionary(device => device.DeviceId);
			using var referencedDeviceIds = new PooledSet<int>();

			var mergeOrGetAllOptions = new MergeableObjectExtensions.MergeOrGetAllOptions() { ReturnUniqueItems = true };

			var usedSourceRoutes = options.Routes.MergeOrGetAll(mergeOrGetAllOptions);

			{
				using var routes = new PooledList<IRoute>(usedSourceRoutes.Length);

				using var routesSet = new PooledSet<IRoute>();
				foreach (var route in usedSourceRoutes)
				{
					if (route is ICombinedRoute combinedRoute)
					{
						foreach (var boundRoute in combinedRoute.GetRoutes())
						{
							if (!routesSet.Add(boundRoute))
							{
								continue;
							}

							routes.Add(boundRoute);
						}
					}
					else
					{
						if (!routesSet.Add(route))
						{
							continue;
						}

						routes.Add(route);
					}
				}

				usedSourceRoutes = [..routes.Span];
				usedSourceRoutes = usedSourceRoutes.MergeOrGetAll(mergeOrGetAllOptions);
			}

			using var buttonRoutes = usedSourceRoutes.OfType<ButtonRoute>().ToPooledList();
			using var axisRoutes = usedSourceRoutes.OfType<AxisRoute>().ToPooledList();
			using var macroRoutes = usedSourceRoutes.OfType<ButtonMacroRoute>().ToPooledList();
			using var axisToButtonRoutes = usedSourceRoutes.OfType<AxisToButtonRoute>().ToPooledList();
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

			var devices = new PooledDictionary<int, TInputDevice>();

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

				using var openedOutputs = CreateOutputDeviceList();

				ImmutableArray<TOutputDevice> outputDevices = [..openedOutputs.Span];
				try
				{
					return new Runtime<TInputDevice, TOutputDevice>(
						options.Name,
						options.DebugLogger,
						devices,
						[..buttonRoutes.Span],
						[..axisRoutes.Span],
						[..macroRoutes.Span],
						[..axisToButtonRoutes.Span],
						[..auxiliaryOutputButtons],
						timeSource,
						outputDevices,
						options.InputSynthesizer);
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
				Runtime<TInputDevice, TOutputDevice>.DisposeDevices(devices.Values);
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

			PooledList<TOutputDevice> CreateOutputDeviceList()
			{
				using var disposables = new PooledList<IDisposable>();
				try
				{
					using var outputRequests = new PooledList<OutputDeviceRequest>(referencedOutputDeviceIds.Count);

					foreach (var deviceId in referencedOutputDeviceIds)
					{
						var buttonRoutesForDevice = buttonRoutes
							.Where(route => route.OutputBinding.OutputDeviceId == deviceId).ToPooledList();
						disposables.Add(buttonRoutesForDevice);
						var axisRoutesForDevice = axisRoutes
							.Where(route => route.OutputBinding.OutputDeviceId == deviceId).ToPooledList();
						disposables.Add(axisRoutesForDevice);
						var macroButtonNumbers = auxiliaryOutputButtons
							.Where(b => b.OutputDeviceId == deviceId)
							.Select(b => b.ButtonNumber)
							.Distinct()
							.ToPooledList();
						disposables.Add(macroButtonNumbers);
						outputRequests.Add(
							new(
								deviceId,
								buttonRoutesForDevice,
								axisRoutesForDevice,
								macroButtonNumbers
							)
						);
					}

					outputRequests.Sort((a, b) => a.DeviceId.CompareTo(b.DeviceId));
					return optionsOutputDeviceFactory.EnumerateConnectedOutputDevices(outputRequests,
						options.ConnectedDevices);
				}
				finally
				{
					foreach (var disposable in disposables)
					{
						disposable.Dispose();
					}
				}
			}
		}
	}
}