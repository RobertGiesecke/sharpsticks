namespace ScaledAxisCSharp.DirectInput;

internal readonly record struct DirectInputDeviceInfo(Guid InstanceGuid, string ProductName, string InstanceName)
{
	public static unsafe DirectInputDeviceInfo FromNative(DirectInputDeviceInstanceNative* native)
	{
		return new DirectInputDeviceInfo(
			native->InstanceGuid,
			ReadNullTerminatedString(native->ProductName, 260),
			ReadNullTerminatedString(native->InstanceName, 260));
	}

	private static unsafe string ReadNullTerminatedString(char* buffer, int capacity)
	{
		var length = 0;
		while (length < capacity && buffer[length] != '\0') length++;

		return new string(buffer, 0, length);
	}
}