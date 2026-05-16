namespace ScaledAxisCSharp.Config;

public static class RuntimeExtensions
{
	extension(AppConfig config)
	{
		/// <summary>
		/// Translate an <see cref="AppConfig"/>'s mappings into the
		/// <see cref="IRoute"/> objects a <see cref="Runtime"/> consumes.
		/// Doesn't enumerate any devices — usable in tests that wire fakes.
		/// </summary>
		/// <param name="deviceMap">
		/// Optional config-id → real-id translation. When supplied, every
		/// <c>DeviceId</c> in bindings (and inside nested modifiers) is
		/// rewritten through this map. Unmapped ids pass through unchanged.
		/// </param>
		public ImmutableArray<IRoute> BuildRoutes(IReadOnlyDictionary<int, int>? deviceMap = null)
		{
			deviceMap ??= EmptyMap;

			var buttonRoutes = new List<ButtonRoute>();
			var axisRoutes = new List<AxisRoute>();

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

		/// <summary>
		/// Yields every <c>DeviceId</c> referenced anywhere in the config —
		/// in bindings and recursively inside modifier nesting.
		/// </summary>
		public IEnumerable<int> EnumerateReferencedDeviceIds()
		{
			foreach (var mapping in config.ButtonMappings)
			{
				yield return mapping.SourceBinding.DeviceId;
			}

			foreach (var mapping in config.AxisMappings)
			{
				yield return mapping.Source.DeviceId;
				if (mapping.Modifier is { } modifier)
				{
					foreach (var id in EnumerateModifierDeviceIds(modifier))
					{
						yield return id;
					}
				}
			}
		}

		/// <summary>
		/// Replace <see cref="AppConfig.Devices"/> with a fresh capture of
		/// every device the bindings reference, populated from the supplied
		/// connected-device list. Call this just before serializing a config
		/// you've built/edited in memory.
		/// </summary>
		public void CaptureDevices(IReadOnlyList<JoystickDevice> connectedDevices)
		{
			var byId = connectedDevices.ToDictionary(static d => d.DeviceId);
			var ids = config.EnumerateReferencedDeviceIds().Distinct().OrderBy(static i => i);

			config.Devices = ids
				.Where(byId.ContainsKey)
				.Select(id =>
				{
					var device = byId[id];
					return new DeviceReference
					{
						DeviceId = id,
						Name = device.Name,
						InstanceGuid = device.InstanceGuid == Guid.Empty ? null : device.InstanceGuid,
					};
				})
				.ToList();
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
		public IReadOnlyDictionary<int, int> ResolveDeviceMap(IReadOnlyList<JoystickDevice> connectedDevices)
		{
			var map = new Dictionary<int, int>();
			if (config.Devices.Count == 0)
			{
				return map;
			}

			var matchedCurrentIds = new HashSet<int>();
			var unmatched = new List<DeviceReference>(config.Devices);

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
				var sortedConfigs = group.OrderBy(static c => c.DeviceId).ToList();
				var sortedCurrents = connectedDevices
					.Where(d => d.Name == group.Key && !matchedCurrentIds.Contains(d.DeviceId))
					.OrderBy(static d => d.DeviceId)
					.ToList();

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

			return map;
		}
	}

	extension(Runtime)
	{
		public static IOutputRuntimeContext BuildFromConfig(AppConfig config)
		{
			var buildOptions = GetBuildOptionsFromConfig(config);
			return Runtime.Build(buildOptions);
		}

		public static RuntimeBuilder.BuildOptions GetBuildOptionsFromConfig(AppConfig config,
			IOutputDeviceFactory? outputDeviceFactory = null)
		{
			using var connectedDevices = PlatformDefaultInputDevice.EnumerateConnected();
			var deviceMap = config.ResolveDeviceMap([..connectedDevices.Select<PlatformDefaultInputDevice, JoystickDevice>(d => d)]);

			return new RuntimeBuilder.BuildOptions
			{
				Name = config.Name ?? "unnamed",
				OutputDeviceFactory = outputDeviceFactory ?? PlatformDefaultOutputDeviceFactory.Instance,
				ConnectedDevices = [..connectedDevices],
				Routes = config.BuildRoutes(deviceMap),
			};
		}
	}

	private static readonly Dictionary<int, int> EmptyMap = new();

	private static int Translate(int id, IReadOnlyDictionary<int, int> map) =>
		map.TryGetValue(id, out var actual) ? actual : id;

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
			WhenButtonPressedAxisModifier when_ => when_ with
			{
				Buttons = [..when_.Buttons.Select(b => b with { DeviceId = Translate(b.DeviceId, map) })],
				WhenPressed = TranslateModifier(when_.WhenPressed, map),
				WhenNotPressed = TranslateModifier(when_.WhenNotPressed, map),
			},
			_ => modifier,  // AxisCurve and friends carry no DeviceIds
		};
	}

	private static IEnumerable<int> EnumerateModifierDeviceIds(IAxisModifier modifier)
	{
		switch (modifier)
		{
			case BlendedAxisCurve blended:
				yield return blended.ModifierAxis.DeviceId;
				break;
			case WhenButtonPressedAxisModifier when_:
				foreach (var button in when_.Buttons)
				{
					yield return button.DeviceId;
				}

				if (when_.WhenPressed is { } whenPressed)
				{
					foreach (var id in EnumerateModifierDeviceIds(whenPressed))
					{
						yield return id;
					}
				}

				if (when_.WhenNotPressed is { } whenNotPressed)
				{
					foreach (var id in EnumerateModifierDeviceIds(whenNotPressed))
					{
						yield return id;
					}
				}

				break;
		}
	}
}
