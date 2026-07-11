namespace SharpSticks.Overlay.WebSockets;

public static class OverlayUtils
{
	public static void ShowServerStatus<TInputDevice>(OverlayServeRuntimeOptions<TInputDevice> runtimeOptions)
		where TInputDevice : JoystickDevice
	{
		Console.WriteLine(
			$"Serving {runtimeOptions.Devices.Length} device(s) on ws://localhost:{runtimeOptions.Port}. Ctrl+C to stop.");
		if (runtimeOptions.WebRoot is not null)
		{
			var relPath = runtimeOptions.WebRootPath is { Length: > 0 } rp
				? "/" + rp.TrimStart('/')
				: "";
			Console.WriteLine(
				$"Overlay: http://localhost:{runtimeOptions.Port}{relPath}   (files from {runtimeOptions.WebRoot})");
		}

		foreach (var device in runtimeOptions.Devices)
		{
			Console.WriteLine(
				$"  \"{device.Name}\" (axes={device.PhysicalAxes.Length}, buttons={device.Capabilities.NumButtons})");
		}
	}
}