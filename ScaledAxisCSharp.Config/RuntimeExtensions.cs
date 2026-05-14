namespace ScaledAxisCSharp.Config;

public static class RuntimeExtensions
{
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
			using var connectedDevices = DirectInputJoystickDevice.EnumerateConnected();
			using var connectedDevicesById = connectedDevices
				.ToPooledDictionary(device => device.DeviceId);
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

				buttonRoutes.Add(new ButtonRoute(
					mapping.SourceBinding,
					new(mapping.VJoyDeviceId ?? config.VJoyDeviceId,
						mapping.TargetButton)));
			}

			foreach (var mapping in config.AxisMappings)
			{
				var source = AxisBinding.Parse(mapping.Source);
				var targetAxis = PhysicalAxis.Parse(mapping.TargetAxis);

				axisRoutes.Add(new AxisRoute
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

			var buildOptions = new RuntimeBuilder.BuildOptions
			{
				Name = config.Name ?? "unnamed",
				OutputDeviceFactory = outputDeviceFactory ?? VJoyDeviceFactory.Instance,
				ConnectedDevices = [..connectedDevices],
				Routes =
				[
					..buttonRoutes,
					..axisRoutes,
				],
			};
			return buildOptions;
		}
	}
}