using System.Collections.Immutable;
using Collections.Pooled;

namespace ScaledAxisCSharp.OutputAbstractions;

public static class RuntimeBuilder
{
	public readonly record struct BuildOptions()
	{
		public required string Name { get; init; }
		public IOutputDeviceFactory? OutputDeviceFactory { get; init; }
		public required ImmutableArray<JoystickDevice> ConnectedDevices { get; init; }
		public ImmutableArray<ButtonRoute> ButtonRoutes { get; init; } = [];
		public ImmutableArray<AxisRoute> AxisRoutes { get; init; } = [];
	}

	extension(Runtime)
	{
		public static IOutputRuntimeContext Build(BuildOptions options)
		{
			var optionsOutputDeviceFactory = options.OutputDeviceFactory ??
			                                 throw new ArgumentNullException(nameof(options.OutputDeviceFactory));

			using var connectedDevicesById = options.ConnectedDevices
				.ToPooledDictionary(device => device.DeviceId);
			var referencedDeviceIds = new HashSet<int>();
			var buttonRoutes = options.ButtonRoutes;
			var axisRoutes = options.AxisRoutes;
			var claimedAxes = new HashSet<(uint OutputDeviceId, PhysicalAxis Axis)>();
			var referencedOutputDeviceIds = new HashSet<uint>();

			foreach (var mapping in buttonRoutes)
			{
				if (mapping.Binding.ButtonNumber < 1)
				{
					throw new InvalidOperationException("Source buttons are 1-based.");
				}

				if (mapping.TargetButton < 1)
				{
					throw new InvalidOperationException("Target buttons are 1-based.");
				}

				if (mapping.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				referencedDeviceIds.Add(mapping.Binding.DeviceId);
				referencedOutputDeviceIds.Add(mapping.OutputDeviceId);
			}

			foreach (var mapping in axisRoutes)
			{
				if (mapping.OutputDeviceId < 1)
				{
					throw new InvalidOperationException("Output device ids are 1-based.");
				}

				if (!claimedAxes.Add((mapping.OutputDeviceId, mapping.OutputAxis)))
				{
					throw new InvalidOperationException(
						$"Target axis '{mapping.OutputAxis}' on Output device {mapping.OutputDeviceId} is mapped more than once.");
				}

				referencedDeviceIds.Add(mapping.Source.DeviceId);
				if (mapping.Modifier is { } m)
				{
					m.FillDevices(referencedDeviceIds);
				}

				referencedOutputDeviceIds.Add(mapping.OutputDeviceId);
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
						buttonRoutes.Where(route => route.OutputDeviceId == deviceId).ToArray(),
						axisRoutes.Where(route => route.OutputDeviceId == deviceId).ToArray()))
					.ToImmutableArray();
				try
				{
					return new Runtime(
						options.Name,
						devices,
						buttonRoutes,
						axisRoutes,
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