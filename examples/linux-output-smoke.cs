// File-based smoke test for the LinuxOutput uinput backend.
// Creates a virtual joystick with X/Y axes and two buttons, jiggles them, then disposes.
// Requires /dev/uinput to be writable by the current user (one-time setup step does this).

#:project ../src/LinuxOutput/LinuxOutput.csproj
#:project ../src/InputAbstractions/InputAbstractions.csproj

using SharpSticks.InputAbstractions;
using SharpSticks.LinuxOutput;
using SharpSticks.OutputAbstractions;

// Hand-build the route lists the factory expects.
const uint deviceId = 1;
var axisRoutes = new[]
{
	new AxisRoute
	{
		Source = new(DeviceId: 99, Axis: Axis.X),
		OutputBinding = new(deviceId, Axis.X),
		Modifier = null,
	},
	new AxisRoute
	{
		Source = new(DeviceId: 99, Axis: Axis.Y),
		OutputBinding = new(deviceId, Axis.Y),
		Modifier = null,
	},
};
var buttonRoutes = new[]
{
	new ButtonRoute(
		new(DeviceId: 99, ButtonNumber: 1),
		new(deviceId, ButtonNumber: 1)),
	new ButtonRoute(
		new(DeviceId: 99, ButtonNumber: 2),
		new(deviceId, ButtonNumber: 2)),
};

OutputDevice device;
try
{
	using var opened = LinuxOutputDeviceFactory.Instance.EnumerateConnectedOutputDevices(
	[
		new(deviceId, buttonRoutes, axisRoutes, []),
	]);
	device = opened[0];
}
catch (InvalidOperationException ex)
{
	Console.WriteLine($"uinput open failed: {ex.Message}");
	return 1;
}

Console.WriteLineInterpolated($"Created virtual device id={deviceId}. Sending 5s of events...");

using (device)
{
	var start = DateTime.UtcNow;
	var count = 0;
	while (DateTime.UtcNow - start < TimeSpan.FromSeconds(5))
	{
		var t = (DateTime.UtcNow - start).TotalSeconds;
		var x = Math.Sin(t * 2.0);
		var y = Math.Cos(t * 2.0);
		device.SetAxisValue(Axis.X, x);
		device.SetAxisValue(Axis.Y, y);
		device.SetButtonState(1, (count / 25) % 2 == 0);
		device.SetButtonState(2, (count / 25) % 2 == 1);
		count++;
		Thread.Sleep(10);
	}

	Console.WriteLineInterpolated($"Sent {count} update bursts. Dropping device.");
}

Console.WriteLine("Done.");
return 0;