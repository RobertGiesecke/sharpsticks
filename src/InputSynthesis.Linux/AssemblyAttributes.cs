using System.Runtime.CompilerServices;

// uinput_setup carries a fixed-size name buffer; LibraryImport needs runtime
// marshalling disabled to marshal it (all our P/Invoke types are blittable).
[assembly: DisableRuntimeMarshalling]
