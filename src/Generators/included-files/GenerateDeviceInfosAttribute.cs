using System;
using System.Diagnostics.CodeAnalysis;
using SharpSticks.InputAbstractions;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class GenerateDeviceInfosAttribute : Attribute
{
	public GenerateDeviceInfosLevels Levels { get; }
	public GenerateDeviceInfosAttribute(GenerateDeviceInfosLevels levels = GenerateDeviceInfosLevels.DeviceNames)
	{
		Levels = levels;
	}
}

[Flags]
internal enum GenerateDeviceInfosLevels
{
	None = 0,
	DeviceNames = 1,
	DeviceIds = 2,
	OutputDeviceIds = 4,
	TypedDevices = 8,
	All = DeviceNames | DeviceIds | OutputDeviceIds | TypedDevices,
}

[ExcludeFromCodeCoverage]
[ AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
internal sealed class RenameDeviceAttribute : Attribute
{
	public string DeviceName { get; }
	public string NewName { get; }
	public RenameDeviceAttribute(string deviceName, string newName)
	{
		DeviceName = deviceName;
		NewName = newName;
	}
}

[ExcludeFromCodeCoverage]
[ AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
internal sealed class RenameButton : Attribute
{
	public string DeviceName { get; }
	public int Button { get; }
	public string NewName { get; }
	public RenameButton(string deviceName, int button, string newName)
	{
		DeviceName = deviceName;
		Button = button;
		NewName = newName;
	}
}

// Preset button-count tiers for [OutputDevice], as an alternative to a raw Buttons count. The
// values are the Linux/uinput ceilings for each evdev button-code block, chosen so button N maps
// to the Nth code a joystick consumer (joydev/SDL) enumerates. On a platform with a flat button
// space (e.g. vJoy = 128) a tier simply requests that many buttons.
// Keep these in sync with SharpSticks.LinuxOutput.LinuxOutputAxisCodes (the mapping authority).
internal enum OutputButtonCapacity : uint
{
	/// No preset — use the explicit Buttons count (the default).
	None = 0,

	/// 32 buttons — BTN_JOYSTICK + BTN_GAMEPAD (0x120-0x13f). Recognized by every driver and game.
	Standard = 32,

	/// 72 buttons — adds the BTN_TRIGGER_HAPPY overflow range the kernel provides for many-button
	/// devices. Still a plain joystick everywhere.
	Extended = 72,

	/// 88 buttons — also uses the generic BTN_MISC/BTN_0.. codes. The Linux ceiling before a device
	/// that also carries axes would be reclassified as a pointer/keyboard.
	Maximum = 88,
}

// Declares an output device by id with a minimum set of capabilities. The code-gen renames
// the device to CodeName and guarantees at least these axes/buttons; if the platform enumerates
// an output device with the same id, its capabilities are merged in (union of axes, max buttons).
// The real device name stays whatever the platform default is. Set either Buttons (a raw count)
// or ButtonCapacity (a preset tier) — ButtonCapacity wins when it is not None.
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
internal sealed class OutputDeviceAttribute : Attribute
{
	public uint DeviceId { get; }
	public string? CodeName { get; set; }
	public Axis[] Axes { get; set; } = [];
	public uint Buttons { get; set; }
	public OutputButtonCapacity ButtonCapacity { get; set; }
	public OutputDeviceAttribute(uint deviceId)
	{
		DeviceId = deviceId;
	}
}

[ExcludeFromCodeCoverage]
[ AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
internal sealed class RenameAxis : Attribute
{
	public string DeviceName { get; }
	public Axis Axis { get; }
	public string NewName { get; }
	public RenameAxis(string deviceName, Axis axis, string newName)
	{
		DeviceName = deviceName;
		Axis = axis;
		NewName = newName;
	}
}