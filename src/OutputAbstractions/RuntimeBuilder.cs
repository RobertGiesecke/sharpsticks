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

		/// <summary>
		/// Whether <see cref="Runtime{TInputDevice,TOutputDevice}.Run"/> calls
		/// <see cref="IInputSynthesizer.EnsureInitialized"/> as it starts, so the
		/// backend (e.g. the Linux uinput device) is created up front and any failure
		/// surfaces at startup. Set false to defer setup to first use — e.g. a
		/// joystick-only profile that never wants the synthetic device created.
		/// Default true; no-op when there is no synthesizer.
		/// </summary>
		public bool InitializeInputSynthesizer { get; init; } = true;
		public required ImmutableArray<TInputDevice> ConnectedDevices { get; init; }
		public ImmutableArray<IConfigurableRoute> Routes { get; init; } = [];
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
			// An explicit synthesizer wins; otherwise take the output backend's
			// platform default (vJoy → SendInput, uinput → uinput).
			var inputSynthesizer = options.InputSynthesizer ?? optionsOutputDeviceFactory.InputSynthesizer;

			using var connectedDevicesById = options.ConnectedDevices
				.ToPooledDictionary(device => device.DeviceId);
			using var referencedDeviceIds = new PooledSet<int>();

			var mergeOrGetAllOptions = new MergeableObjectExtensions.MergeOrGetAllOptions() { ReturnUniqueItems = true };

			var usedSourceRoutes = options.Routes.CastArray<IRoute>().MergeOrGetAll(mergeOrGetAllOptions);

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

			using var buttonToTargetRoutes = usedSourceRoutes.OfType<ButtonToTargetRoute>().ToPooledList();
			using var axisRoutes = usedSourceRoutes.OfType<AxisRoute>().ToPooledList();
			using var macroRoutes = usedSourceRoutes.OfType<ButtonMacroRoute>().ToPooledList();
			using var axisZoneRoutes = usedSourceRoutes.OfType<AxisZoneRoute>().ToPooledList();
			using var axisToMouseRoutes = usedSourceRoutes.OfType<AxisToMouseRoute>().ToPooledList();
			using var axisToScrollRoutes = usedSourceRoutes.OfType<AxisToScrollRoute>().ToPooledList();
			using var claimedAxes = new PooledSet<(uint OutputDeviceId, Axis Axis)>();
			using var referencedOutputDeviceIds = new PooledSet<uint>();
			using var auxiliaryOutputButtons = new PooledSet<OutputButtonBinding>();

			foreach (var route in buttonToTargetRoutes)
			{
				if (route.Source.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Source buttons are 1-based.");
				}

				referencedDeviceIds.Add(route.Source.DeviceId);

				// Only vJoy targets reference an output device; key/mouse/scroll go to the synthesizer.
				if (route.Target is OutputButtonBinding outputButton)
				{
					if (outputButton.ButtonNumber < 1)
					{
						throw new InvalidOperationException("Target buttons are 1-based.");
					}

					if (outputButton.OutputDeviceId < 1)
					{
						throw new InvalidOperationException("Output device ids are 1-based.");
					}

					referencedOutputDeviceIds.Add(outputButton.OutputDeviceId);
				}
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

			foreach (var route in axisZoneRoutes)
			{
				if (route.Max < route.Min)
				{
					throw new InvalidOperationException(
						$"AxisZoneRoute: Max ({route.Max}) must be >= Min ({route.Min}).");
				}

				if (route.Mode == AxisZoneTriggerMode.Pulse && route.PulseDuration <= TimeSpan.Zero)
				{
					throw new InvalidOperationException(
						"AxisZoneRoute.PulseDuration must be positive when Mode is Pulse.");
				}

				referencedDeviceIds.Add(route.Source.DeviceId);

				// A vJoy-targeted zone with no button source still needs the device to expose
				// the button, so register it as an auxiliary output.
				if (route.Target is OutputButtonBinding outputButton)
				{
					if (outputButton.OutputDeviceId < 1)
					{
						throw new InvalidOperationException("Output device ids are 1-based.");
					}

					if (outputButton.ButtonNumber < 1)
					{
						throw new InvalidOperationException("Target buttons are 1-based.");
					}

					auxiliaryOutputButtons.Add(outputButton);
				}
			}

			foreach (var route in axisToMouseRoutes)
			{
				referencedDeviceIds.Add(route.Source.DeviceId);
				route.Modifier?.FillDevices(referencedDeviceIds);
			}

			foreach (var route in axisToScrollRoutes)
			{
				referencedDeviceIds.Add(route.Source.DeviceId);
				route.Modifier?.FillDevices(referencedDeviceIds);
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
						[..buttonToTargetRoutes.Span],
						[..axisRoutes.Span],
						[..macroRoutes.Span],
						[..axisZoneRoutes.Span],
						[..axisToMouseRoutes.Span],
						[..axisToScrollRoutes.Span],
						[..auxiliaryOutputButtons],
						timeSource,
						outputDevices,
						inputSynthesizer,
						options.InitializeInputSynthesizer);
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
						var outputButtonsForDevice = buttonToTargetRoutes
							.Select(route => route.Target)
							.OfType<OutputButtonBinding>()
							.Where(button => button.OutputDeviceId == deviceId)
							.ToPooledList();
						disposables.Add(outputButtonsForDevice);
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
								outputButtonsForDevice,
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