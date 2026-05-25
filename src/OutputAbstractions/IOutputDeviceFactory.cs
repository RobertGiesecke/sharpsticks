namespace SharpSticks.OutputAbstractions;

public interface IOutputDeviceFactory<T> : IOutputDeviceFactory
	where T : OutputDevice
{
	/// <inheritdoc cref="IOutputDeviceFactory.EnumerateConnectedOutputDevices"/>" />
	new PooledList<T> EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs);

	PooledList<OutputDevice> IOutputDeviceFactory.EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs)
	{
		using var list = EnumerateConnectedOutputDevices(requests, availableInputs);
		var result = new PooledList<OutputDevice>(list.Count);
		try
		{
			foreach (var device in list)
			{
				result.Add(device);
			}

			return result;
		}
		catch
		{
			result.Dispose();
			throw;
		}
	}
}

public interface IOutputDeviceFactory
{
	/// <summary>
	/// Open / acquire every requested output device in a single batch. Backends that
	/// surface as input devices (vJoy on Windows) walk <paramref name="requests"/> and
	/// <paramref name="availableInputs"/> together and assign each new output to its
	/// matching input counterpart sequentially — claiming inputs from the front of the
	/// candidate pool so non-contiguous DeviceIds don't break the indexing. Backends that
	/// can't observe their counterpart at create time (Linux uinput) ignore
	/// <paramref name="availableInputs"/> and leave <c>InputDeviceId</c> null.
	/// </summary>
	/// <remarks>The returned <see cref="PooledList{T}"/> is owned by the caller — dispose
	/// after extracting / consuming. On partial failure mid-batch the factory disposes
	/// any outputs it already created before rethrowing.</remarks>
	PooledList<OutputDevice> EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs);

	/// Non-claiming metadata snapshot of every available output slot. Used at design
	/// time (e.g. by the source generator). Backends that materialize devices on demand
	/// (Linux uinput) return empty.
	ImmutableArray<AvailableOutputDevice> EnumerateAvailableOutputs() => ImmutableArray<AvailableOutputDevice>.Empty;
}