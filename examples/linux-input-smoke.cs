// File-based smoke test for the LinuxInput evdev backend.
// Enumerates joystick/gamepad devices, waits ~10s on the first one for input
// events, and prints whatever state changes the kernel reports.

#:project ../src/LinuxInput/LinuxInput.csproj

using SharpSticks.InputAbstractions;
using SharpSticks.LinuxInput;

using var devices = LinuxInputJoystickDevice.EnumerateConnected();
Console.WriteLineInterpolated($"Found {devices.Count} joystick/gamepad device(s):");
foreach (var device in devices)
{
	Console.WriteLineInterpolated($"  [{device.DeviceId}] {device.Name}");
	Console.WriteLineInterpolated($"      instance = {device.InstanceName}  guid = {device.InstanceGuid}");
	Console.WriteLine($"      axes     = {string.Join(", ", device.PhysicalAxes)}");
	Console.WriteLineInterpolated($"      buttons  = {device.Capabilities.NumButtons}");
}

if (devices.Count == 0)
{
	Console.WriteLine("No joysticks found. Make sure your user is in the `input` group.");
	return;
}

var primary = devices[0];
Console.WriteLine($"\nWatching {primary.Name} for 10s — press buttons or move sticks...");

JoystickState previous = default;
var deadline = DateTime.UtcNow.AddSeconds(10);
int wakes = 0, timeouts = 0, stateChanges = 0;
while (DateTime.UtcNow < deadline)
{
	if (!primary.DataAvailable.WaitOne(TimeSpan.FromMilliseconds(500)))
	{
		timeouts++;
		continue;
	}

	wakes++;
	if (!primary.TryReadState(out var state, out var error))
	{
		Console.WriteLine($"Read error: {error}");
		continue;
	}

	if (state != previous)
	{
		stateChanges++;
		Console.WriteLine($"  X={state.X,6}  Y={state.Y,6}  Z={state.Z,6}  " +
			$"Rx={state.Rx,6}  Ry={state.Ry,6}  Rz={state.Rz,6}  " +
			$"btn={state.ButtonBitsLow:x16}");
		previous = state;
	}
}
Console.WriteLineInterpolated($"wakes={wakes}  timeouts={timeouts}  stateChanges={stateChanges}");

Console.WriteLine("Done.");
