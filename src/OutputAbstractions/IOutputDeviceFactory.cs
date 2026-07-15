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
	/// (Linux uinput) return only the devices that happen to be live right now.
	ImmutableArray<AvailableOutputDevice> EnumerateAvailableOutputs() => ImmutableArray<AvailableOutputDevice>.Empty;

	/// Resolve the identity a declared output device id would carry once materialized, without
	/// creating it. Lets the source generator give a declared <c>[OutputDevice]</c> the same name
	/// and input-side product GUID the running device reports, so the input-side loopback is
	/// recognized as the same device rather than listed twice. Backends that can't predict an
	/// identity echo the request with <see cref="Guid.Empty"/> and an empty name.
	AvailableOutputDevice DescribeDeclaredOutput(uint deviceId, ImmutableArray<Axis> axes, uint buttonCount) =>
		new(deviceId, axes, buttonCount, Guid.Empty, string.Empty);

	/// <summary>
	/// Platform sink for synthesized keyboard/mouse events (the macro key/mouse
	/// actions drive it), or <c>null</c> when this backend doesn't synthesize input.
	/// The runtime uses it as the default synthesizer unless the build supplies one.
	/// </summary>
	IInputSynthesizer? InputSynthesizer { get; }
}