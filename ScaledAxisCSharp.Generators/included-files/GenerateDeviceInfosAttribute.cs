using System;
using ScaledAxisCSharp.InputAbstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
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

[ AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
internal sealed class RenameAxis : Attribute
{
	public string DeviceName { get; }
	public PhysicalAxis Axis { get; }
	public string NewName { get; }
	public RenameAxis(string deviceName, PhysicalAxis axis, string newName)
	{
		DeviceName = deviceName;
		Axis = axis;
		NewName = newName;
	}
}