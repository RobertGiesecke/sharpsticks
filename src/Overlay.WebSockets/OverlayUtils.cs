namespace SharpSticks.Overlay.WebSockets;

public static class OverlayUtils
{
	extension(JoystickDevice device)
	{
		/// <summary>
		/// Whether this device is the input-side mirror of a virtual output device
		/// (vJoy's DirectInput entry, a SharpSticks uinput node). Recognized by HID
		/// identity via <see cref="VirtualOutputProducts"/>, with a name fallback for
		/// vJoy entries that don't carry the product guid (e.g. test fakes).
		/// </summary>
		public bool IsVirtualOutputMirror =>
			VirtualOutputProducts.IsVirtualOutput(device.ProductGuid)
			|| device.Name.StartsWith("vJoy", StringComparison.OrdinalIgnoreCase);
	}

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