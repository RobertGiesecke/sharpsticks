namespace SharpSticks.Config;

public static class RuntimeExtensions
{
	extension(AppConfig config)
	{
		/// <summary>
		/// Translate an <see cref="AppConfig"/>'s mappings into the
		/// <see cref="IBoundRoute"/> objects a <see cref="Runtime"/> consumes.
		/// Doesn't enumerate any devices — usable in tests that wire fakes.
		/// </summary>
		/// <param name="deviceMap">
		/// Optional config-id → real-id translation. When supplied, every
		/// <c>DeviceId</c> in bindings (and inside nested modifiers) is
		/// rewritten through this map. Unmapped ids pass through unchanged.
		/// </param>
		public ImmutableArray<IBoundRoute> BuildRoutes(IReadOnlyDictionary<int, int>? deviceMap = null)
		{
			deviceMap ??= EmptyMap;

			using var buttonRoutes = new PooledList<ButtonRoute>();
			using var axisRoutes = new PooledList<AxisRoute>();

			foreach (var mapping in config.ButtonMappings)
			{
				if (mapping.SourceBinding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Source buttons are 1-based.");
				}

				if (mapping.TargetButton < 1)
				{
					throw new InvalidOperationException("Target buttons are 1-based.");
				}

				var source = mapping.SourceBinding with
				{
					DeviceId = Translate(mapping.SourceBinding.DeviceId, deviceMap),
				};
				buttonRoutes.Add(new(
					source,
					new(mapping.VJoyDeviceId ?? config.VJoyDeviceId,
						mapping.TargetButton)));
			}

			foreach (var mapping in config.AxisMappings)
			{
				var parsedSource = AxisBinding.Parse(mapping.Source);
				var source = parsedSource with
				{
					DeviceId = Translate(parsedSource.DeviceId, deviceMap),
				};
				var targetAxis = Axis.Parse(mapping.TargetAxis);

				axisRoutes.Add(new()
				{
					Source = source,
					OutputBinding = new(
						mapping.OutputDeviceId ?? config.VJoyDeviceId,
						targetAxis),
					Scale = mapping.Scale,
					Offset = mapping.Offset,
					Modifier = TranslateModifier(mapping.Modifier, deviceMap),
				});
			}

			return
			[
				..buttonRoutes,
				..axisRoutes,
			];
		}

		private void FillDevices(ICollection<int> deviceIds)
		{
			foreach (var mapping in config.ButtonMappings)
			{
				deviceIds.Add(mapping.SourceBinding.DeviceId);
			}

			foreach (var mapping in config.AxisMappings)
			{
				deviceIds.Add(mapping.Source.DeviceId);
				if (mapping.Modifier is not { } modifier)
				{
					continue;
				}

				modifier.FillDevices(deviceIds);
			}
		}

		/// <summary>
		/// Replace <see cref="AppConfig.Devices"/> with a fresh capture of
		/// every device the bindings reference, populated from the supplied
		/// connected-device list. Call this just before serializing a config
		/// you've built/edited in memory.
		/// </summary>
		public void CaptureDevices<TInputDevice>(IReadOnlyList<TInputDevice> connectedDevices)
			where TInputDevice : JoystickDevice
		{
			using var byId = connectedDevices.ToPooledDictionary(static d => d.DeviceId);

			using var captured = new PooledList<DeviceReference>();
			using var deviceIds = new PooledSet<int>();
			config.FillDevices(deviceIds);
			using var sortedDeviceIds = deviceIds.ToPooledList();
			sortedDeviceIds.Sort();
			foreach (var id in sortedDeviceIds)
			{
				if (!byId.TryGetValue(id, out var device))
				{
					continue;
				}

				captured.Add(new()
				{
					DeviceId = id,
					Name = device.Name,
					InstanceGuid = device.InstanceGuid == Guid.Empty ? null : device.InstanceGuid,
				});
			}

			config.Devices = [..captured];
		}

		/// <summary>
		/// Build a config-id → real-id map by matching every entry in
		/// <see cref="AppConfig.Devices"/> against <paramref name="connectedDevices"/>.
		/// Tier 1: <see cref="DeviceReference.InstanceGuid"/> exact match.
		/// Tier 2: for the leftovers, pair config entries against connected
		/// devices sharing the same <see cref="JoystickDevice.Name"/>,
		/// matching by ascending <c>DeviceId</c> on each side.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Thrown when a config entry can't be paired (no same-name device
		/// left to match it).
		/// </exception>
		public void ResolveDeviceMap<TInputDevice>(
			IReadOnlyList<TInputDevice> connectedDevices,
			IDictionary<int, int> map)
			where TInputDevice : JoystickDevice
		{
			if (config.Devices.Count == 0)
			{
				return;
			}

			using var matchedCurrentIds = new PooledSet<int>();
			using var unmatched = new PooledList<DeviceReference>(config.Devices);

			// Tier 1: InstanceGuid exact matches.
			for (var i = unmatched.Count - 1; i >= 0; i--)
			{
				var entry = unmatched[i];
				if (entry.InstanceGuid is not { } guid || guid == Guid.Empty)
				{
					continue;
				}

				var match = connectedDevices.FirstOrDefault(d =>
					d.InstanceGuid != Guid.Empty
					&& d.InstanceGuid == guid
					&& !matchedCurrentIds.Contains(d.DeviceId));
				if (match is null)
				{
					continue;
				}

				map[entry.DeviceId] = match.DeviceId;
				matchedCurrentIds.Add(match.DeviceId);
				unmatched.RemoveAt(i);
			}

			// Tier 2: same-name groups, paired by ascending DeviceId.
			foreach (var group in unmatched.GroupBy(static c => c.Name, StringComparer.Ordinal))
			{
				using var sortedConfigs = group.OrderBy(static c => c.DeviceId).ToPooledList();
				using var sortedCurrents = connectedDevices
					// ReSharper disable once AccessToDisposedClosure
					.Where(d => d.Name == group.Key && !matchedCurrentIds.Contains(d.DeviceId))
					.OrderBy(static d => d.DeviceId)
					.ToPooledList();

				for (var i = 0; i < sortedConfigs.Count; i++)
				{
					if (i >= sortedCurrents.Count)
					{
						throw new InvalidOperationException(
							$"Config references device '{group.Key}' (configured id {sortedConfigs[i].DeviceId}) but no matching connected device is available.");
					}

					map[sortedConfigs[i].DeviceId] = sortedCurrents[i].DeviceId;
					matchedCurrentIds.Add(sortedCurrents[i].DeviceId);
				}
			}

			return;
		}
	}

	extension<TInputDevice, TOutputDevice>(Runtime<TInputDevice, TOutputDevice>)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public static IOutputRuntimeContext<TInputDevice, TOutputDevice> BuildFromConfig(AppConfig config,
			IOutputDeviceFactory<TOutputDevice> outputDeviceFactory,
			IJoystickDeviceFactory<TInputDevice> joystickDeviceFactory)
		{
			var buildOptions = GetBuildOptionsFromConfig(
				config,
				outputDeviceFactory, joystickDeviceFactory);
			return Runtime<TInputDevice, TOutputDevice>.Build(buildOptions);
		}

		public static RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice> GetBuildOptionsFromConfig(
			AppConfig config,
			IOutputDeviceFactory<TOutputDevice> outputDeviceFactory,
			IJoystickDeviceFactory<TInputDevice> joystickDeviceFactory)
		{
			using var connectedDevices = joystickDeviceFactory.EnumerateConnectedInputDevices();

			using var deviceMap = new PooledDictionary<int, int>();

			config.ResolveDeviceMap(connectedDevices, deviceMap);

			return new()
			{
				Name = config.Name ?? "unnamed",
				OutputDeviceFactory = outputDeviceFactory,
				ConnectedDevices = [..connectedDevices],
				Routes = config.BuildRoutes(deviceMap),
			};
		}
	}

	extension <TInputDevice, TOutputDevice>(ICombinedDeviceFactory<TInputDevice, TOutputDevice> factory) 
		where TInputDevice : JoystickDevice 
		where TOutputDevice : OutputDevice
	{
		public IOutputRuntimeContext<TInputDevice, TOutputDevice> RuntimeFromConfig(
			AppConfig config)
		{
			var buildOptions = GetBuildOptionsFromConfig(
				config,
				factory,
				factory);
			return Runtime<TInputDevice, TOutputDevice>.Build(buildOptions);
		}

		public RuntimeBuilder.BuildOptions<TInputDevice, TOutputDevice> GetBuildOptionsFromConfig(AppConfig config)
		{
			using var connectedDevices = factory.EnumerateConnectedInputDevices();

			using var deviceMap = new PooledDictionary<int, int>();

			config.ResolveDeviceMap(connectedDevices, deviceMap);

			return new()
			{
				Name = config.Name ?? "unnamed",
				OutputDeviceFactory = factory,
				ConnectedDevices = [..connectedDevices],
				Routes = config.BuildRoutes(deviceMap),
			};
		}
	}

	private static readonly Dictionary<int, int> EmptyMap = new();

	private static int Translate(int id, IReadOnlyDictionary<int, int> map) =>
		map.GetValueOrDefault(id, id);

	private static IAxisModifier? TranslateModifier(IAxisModifier? modifier, IReadOnlyDictionary<int, int> map)
	{
		if (modifier is null || map.Count == 0)
		{
			return modifier;
		}

		return modifier switch
		{
			BlendedAxisCurve blended => blended with
			{
				ModifierAxis = blended.ModifierAxis with
				{
					DeviceId = Translate(blended.ModifierAxis.DeviceId, map),
				},
			},
			WhenButtonPressedAxisModifier whenBtnPressed => whenBtnPressed with
			{
				Buttons = [..whenBtnPressed.Buttons.Select(b => b with { DeviceId = Translate(b.DeviceId, map) })],
				WhenPressed = TranslateModifier(whenBtnPressed.WhenPressed, map),
				WhenNotPressed = TranslateModifier(whenBtnPressed.WhenNotPressed, map),
			},
			_ => modifier, // AxisCurve and friends carry no DeviceIds
		};
	}
}