namespace SharpSticks.InputAbstractions;

public sealed record ButtonsRoutedToOutput<TDevice> : ICombinedRoute
	where TDevice : JoystickDevice
{
	public required TDevice Device { get; init; }
	public required uint OutputDeviceId { get; init; }
	public Func<TDevice, ButtonBinding, bool>? Predicate { get; init; }

	IEnumerable<IRoute> ICombinedRoute.GetRoutes()
	{
		for (var i = 0; i < Device.Capabilities.NumButtons; i++)
		{
			var binding = Device.BindButton(i + 1);

			if (Predicate?.Invoke(Device, binding) is false)
			{
				continue;
			}

			yield return binding.RouteTo(new(OutputDeviceId, binding.ButtonNumber));
		}
	}
}