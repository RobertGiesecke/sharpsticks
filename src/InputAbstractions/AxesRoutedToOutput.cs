namespace SharpSticks.InputAbstractions;

public sealed record AxesRoutedToOutput<TDevice> : ICombinedRoute
	where TDevice : JoystickDevice
{
	public required TDevice Device { get; init; }
	public required uint OutputDeviceId { get; init; }
	public Func<TDevice, AxisBinding, bool>? Predicate { get; init; }
	public Func<TDevice, AxisBinding, RouteAxisOptions?>? OptionsCallback { get; init; }

	IEnumerable<IRoute> ICombinedRoute.GetRoutes()
	{
		for (var i = 0; i < Device.Capabilities.NumAxes; i++)
		{
			var axisType = Device.PhysicalAxes[i];

			var axisBinding = Device.BindAxis(axisType);
			if (Predicate?.Invoke(Device, axisBinding) is false)
			{
				continue;
			}

			if (OptionsCallback?.Invoke(Device, axisBinding) is not { } options)
			{
				options = new();
			}

			yield return axisBinding.RouteToSameAxisOnOutput(OutputDeviceId, options);
		}
	}
}