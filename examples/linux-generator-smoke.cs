// File-based program that exercises the source generator on Linux against the PS4
// controller plugged into the test VM. We don't actually run anything — we just want
// the generator to fire so its .g.cs output is on disk under obj/generated/, where
// we can inspect it and verify the input enumeration + rename machinery works on
// Linux exactly as on Windows.
//
// Run as:
//   dotnet build examples/linux-generator-smoke.cs -p:EmitCompilerGeneratedFiles=true
// then inspect the file at obj/generated/SharpSticks.Generators/.../Devices.DeviceInfos.g.cs

#:project ../src/Console/Console.csproj

using SharpSticks.InputAbstractions;

[assembly: GenerateDeviceInfos(GenerateDeviceInfosLevels.All)]

// The PS4 controller's evdev product name is "Sony Computer Entertainment Wireless Controller".
// After ToIdentifier it becomes a single long PascalCase string. We rename it to "DualShock4"
// using the GENERATED-CONST symbol so the generator must resolve the symbol identifier (not the
// resolved string value) to find the right device.
[assembly: RenameDevice(DeviceNames.SonyComputerEntertainmentWirelessController, "DualShock4")]
[assembly: RenameButton(DeviceNames.DualShock4, 1, "Cross")]
[assembly: RenameAxis(DeviceNames.DualShock4, Axis.X, "LeftStickX")]

// Empty entry point — nothing to do at runtime.
return 0;

partial class Devices;
