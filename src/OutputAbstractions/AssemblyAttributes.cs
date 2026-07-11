using System.Runtime.CompilerServices;

// Backend assemblies and test fixtures access internal members of the output abstractions
// (e.g. constructing backend-specific output devices). OutputDevice.InputDeviceId is now
// supplied via the constructor rather than an internal setter.
[assembly: InternalsVisibleTo("SharpSticks.VJoy")]
[assembly: InternalsVisibleTo("SharpSticks.LinuxOutput")]
[assembly: InternalsVisibleTo("SharpSticks.Testing")]
