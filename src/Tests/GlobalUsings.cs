global using System.Collections.Immutable;
global using System.Diagnostics.CodeAnalysis;
global using System.Text.Json;
global using Collections.Pooled;
global using SharpSticks.Config;
global using SharpSticks.InputAbstractions;
global using SharpSticks.InputAbstractions.Keyboard;
global using SharpSticks.InputAbstractions.Mouse;
global using SharpSticks.OutputAbstractions;
global using SharpSticks.Testing;
global using Xunit;

global using IFakesOutputRuntimeContext = SharpSticks.OutputAbstractions.IOutputRuntimeContext<SharpSticks.Testing.FakeJoystickDevice, SharpSticks.Testing.FakeOutputDevice>;
global using FakesRuntime = SharpSticks.OutputAbstractions.Runtime<SharpSticks.Testing.FakeJoystickDevice, SharpSticks.Testing.FakeOutputDevice>;