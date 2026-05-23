using System.Runtime.CompilerServices;

// Backend assemblies need to write the OutputDevice.InputDeviceId internal setter when
// they pair a created output to its corresponding input. Tests fakes can read/write it
// directly when constructing fixtures.
[assembly: InternalsVisibleTo("SharpSticks.VJoy")]
[assembly: InternalsVisibleTo("SharpSticks.LinuxOutput")]
[assembly: InternalsVisibleTo("SharpSticks.Testing")]
