using Collections.Pooled;
using SharpSticks.InputAbstractions;

namespace SharpSticks.OutputAbstractions;

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
	PooledList<OutputDevice> Open(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs);
}
