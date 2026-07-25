namespace SharpSticks.Overlay.Integration;

public readonly record struct OverlayServeOptions
{
	public bool DontServeOverlay { get; init; }

	/// <summary>
	/// Specifies the root directory from which the overlay's static files will be served.
	/// This property determines the base path for locating the resources needed
	/// by the overlay server during its runtime operation.
	/// </summary>
	public string? WebRoot { get; init; }

	public string? WebRootPath { get; init; }
	public ushort? Port { get; init; }
}

public static class OverlayServerExtensions
{
	/// <param name="runtimeContext">The runtime context managing the input and output devices.</param>
	/// <typeparam name="TInputDevice">The type of input device, which must inherit from <see cref="JoystickDevice"/> and implement <see cref="IJoystickDeviceWithFactory{TSelf}"/>.</typeparam>
	/// <typeparam name="TOutputDevice">The type of output device, which must inherit from <see cref="OutputDevice"/>.</typeparam>
	extension<TInputDevice, TOutputDevice>(IOutputRuntimeContext<TInputDevice, TOutputDevice> runtimeContext)
		where TInputDevice : JoystickDevice, IJoystickDeviceWithFactory<TInputDevice>
		where TOutputDevice : OutputDevice
	{
		/// <summary>
		/// Sets up and starts an overlay server for the specified input and output devices if the given options allow it.
		/// </summary>
		/// <param name="overlayOptions">Configuration options specifying whether the overlay server should be served and its settings.</param>
		/// <returns>An instance of <see cref="IRuntimeEventInstance"/> representing the runtime event associated with serving the overlay server, or null if the server is not started.</returns>
		public IRuntimeEventInstance? ServeOverlay(OverlayServeOptions overlayOptions)
		{
			if (overlayOptions is not { DontServeOverlay: false })
			{
				return null;
			}

			return runtimeContext.NewRunEvent((args, ct) =>
			{
				var usedDevices = args.Runtime.GetAllDevices();

				return Task.Run(() => OverlayServer.Serve<TInputDevice>(new()
					{
						WebRoot = overlayOptions.WebRoot,
						WebRootPath = overlayOptions.WebRootPath,
						Port = overlayOptions.Port ?? OverlayServer.DefaultPort,
						Devices = usedDevices,
						// ReSharper disable once AccessToDisposedClosure
					}, new OverlayServeEvents<TInputDevice>()
					{
						Started = OverlayUtils.ShowServerStatus,
					}, ct),
					ct);
			}).WithAfterRun((args, _) => args.State.GetAwaiter().GetResult());
		}
	}
}