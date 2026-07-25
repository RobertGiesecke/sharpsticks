namespace SharpSticks.OutputAbstractions;

public static class RuntimeContextExtensions
{
	extension<TInputDevice, TOutputDevice>(IOutputRuntimeContext<TInputDevice, TOutputDevice> runtimeContext)
		where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
		where TOutputDevice : OutputDevice
	{
		/// <summary>
		/// Gets all used input devices, incl those that are the used output devices
		/// </summary>
		public ImmutableArray<TInputDevice> GetAllDevices()
		{
			if (runtimeContext.OutputDevices is { IsDefaultOrEmpty: true })
			{
				return runtimeContext.Devices;
			}

			using var outputDeviceIds = runtimeContext.OutputDevices.Select(t => t.InputDeviceId).ToPooledSet();
			foreach (var inputDevice in runtimeContext.Devices)
			{
				outputDeviceIds.Remove(inputDevice.DeviceId);
			}

			var usedDevices = runtimeContext.Devices;

			using var allDevices = TInputDevice.Factory.EnumerateConnectedInputDevices();
			using var addedDevices = new PooledList<TInputDevice>(usedDevices.Length + outputDeviceIds.Count);
			usedDevices.CopyTo(addedDevices.AddSpan(usedDevices.Length));
			foreach (var joystickDevice in allDevices)
			{
				if (outputDeviceIds.Remove(joystickDevice.DeviceId))
				{
					addedDevices.Add(joystickDevice);
				}

				if (outputDeviceIds.Count < 1)
				{
					break;
				}
			}

			return [.. addedDevices.Span];
		}
	}
}