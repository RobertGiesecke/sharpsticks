# SharpSticks

An experiment in building low-latency [vJoy](https://github.com/njz3/vJoy) configurations purely from C# code, with heavy assistance from a Roslyn source generator that materialises a strongly-typed `Devices` / `Typed` surface so scripts can refer to axes and buttons by name (`RightStick.Axes.X`, `VJoy1.Buttons.Fire`).

See [`examples/`](examples/) for working scripts.

## Prerequisites

Any version of vJoy, and the .NET 10 SDK:

```
winget install -e --id ShaulEizikovich.vJoyDeviceDriver
winget install -e --id Microsoft.DotNet.SDK.10
```

If you don't already have a modern C# editor, install VS Code:

```
winget install -e --id Microsoft.VisualStudioCode
```

or download it from <https://code.visualstudio.com/>.

## Writing a script

Create a new `.cs` file (e.g. `your-game.cs`) and open it in VS Code. Put this as the first line — it pulls down the package and everything needed for the editor to give you full IntelliSense over the typed device surface:

```csharp
#:package SharpSticks.Console@0.1.0-debug02
```

Run it with `dotnet run your-game.cs`.

Create a small standalone exe (no dependency on dotnet) with `dotnet publish your-game.cs`.

