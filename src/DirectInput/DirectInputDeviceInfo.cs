namespace SharpSticks.DirectInput;

public readonly record struct DirectInputDeviceInfo(
	int DeviceId,
	Guid InstanceGuid,
	Guid ProductGuid,
	string ProductName,
	string InstanceName)
{
	internal static unsafe DirectInputDeviceInfo FromNative(int deviceId, DirectInputDeviceInstanceNative* native)
	{
		return new(
			deviceId,
			native->InstanceGuid,
			native->ProductGuid,
			ReadNullTerminatedString(native->ProductName, 260),
			ReadNullTerminatedString(native->InstanceName, 260));
	}

	private static unsafe string ReadNullTerminatedString(char* buffer, int capacity)
	{
		var length = 0;
		while (length < capacity && buffer[length] != '\0') length++;

		return new(buffer, 0, length);
	}
}
