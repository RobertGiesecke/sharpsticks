using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class GenerateDeviceInfosAttribute : Attribute
{
	public GenerateDeviceInfosLevels Levels { get; }
	public GenerateDeviceInfosAttribute(GenerateDeviceInfosLevels levels = GenerateDeviceInfosLevels.DeviceNames)
	{
		Levels = levels;
	}
}

internal enum GenerateDeviceInfosLevels
{
	None = 0,
	DeviceNames = 1,
	DeviceIds = 2,
	OutputDeviceIds = 4,
	TypedDevices = 8,
	All = DeviceNames | DeviceIds | OutputDeviceIds | TypedDevices,
}