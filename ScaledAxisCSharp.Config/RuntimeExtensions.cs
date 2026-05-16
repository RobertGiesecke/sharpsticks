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
		public ImmutableArray<IRoute> BuildRoutes()
		{
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

				buttonRoutes.Add(new(
					mapping.SourceBinding,
					new(mapping.VJoyDeviceId ?? config.VJoyDeviceId,
						mapping.TargetButton)));
			}

			foreach (var mapping in config.AxisMappings)
			{
				var source = AxisBinding.Parse(mapping.Source);
				var targetAxis = Axis.Parse(mapping.TargetAxis);

				axisRoutes.Add(new()
				{
					Source = source,
					OutputBinding = new(
						mapping.OutputDeviceId ?? config.VJoyDeviceId,
						targetAxis),
					Scale = mapping.Scale,
					Offset = mapping.Offset,
					Modifier = mapping.Modifier,
				});
			}

			return
			[
				..buttonRoutes,
				..axisRoutes,
			];
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

			return new RuntimeBuilder.BuildOptions
			{
				Name = config.Name ?? "unnamed",
				OutputDeviceFactory = outputDeviceFactory ?? PlatformDefaultOutputDeviceFactory.Instance,
				ConnectedDevices = [..connectedDevices],
				Routes = config.BuildRoutes(),
			};
		}
	}
}
