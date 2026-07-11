namespace SharpSticks.Overlay.WebSockets;

public interface IOverlayServer
{
	OverlayServeResult Serve<TInputDevice>(IJoystickDeviceFactory<TInputDevice> deviceFactory,
		ServeOptions<TInputDevice> options,
		CancellationToken cancellationToken)
		where TInputDevice : JoystickDevice;
}