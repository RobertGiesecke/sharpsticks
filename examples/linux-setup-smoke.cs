// File-based smoke test for the SharpSticks setup subcommand.
// Run as: doas dotnet run examples/linux-setup-smoke.cs -- setup uinput

#:project ../src/Console/Console.csproj
#:project ../src/LinuxOutput/LinuxOutput.csproj

using SharpSticks.Console;
using SharpSticks.InputAbstractions;
using SharpSticks.LinuxOutput;

const uint deviceId = 1;
var axisRoutes = new[]
{
	new AxisRoute
	{
		Source = new AxisBinding(DeviceId: 99, Axis: Axis.X),
		OutputBinding = new OutputAxisBinding(deviceId, Axis.X),
		Modifier = null,
	},
	new AxisRoute
	{
		Source = new AxisBinding(DeviceId: 99, Axis: Axis.Y),
		OutputBinding = new OutputAxisBinding(deviceId, Axis.Y),
		Modifier = null,
	},
};
var buttonRoutes = new[]
{
	new ButtonBinding(DeviceId: 99, ButtonNumber: 1)
		.RouteTo(new OutputButtonBinding(deviceId, ButtonNumber: 1)),
	new ButtonBinding(DeviceId: 99, ButtonNumber: 2)
		.RouteTo(new OutputButtonBinding(deviceId, ButtonNumber: 2)),
};

BuildAndRunAsConsole(new ConsoleExtensions.BuildOptions
{
	Name = "linux-setup-smoke",
	OutputDeviceFactory = LinuxOutputDeviceFactory.Instance,
	Routes = [..buttonRoutes, ..axisRoutes],
});
