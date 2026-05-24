using System.Runtime.CompilerServices;

namespace SharpSticks.OutputAbstractions;

public abstract class OutputDevice : IDisposable, IOutputDevice
{
	protected OutputDevice(uint deviceId)
	{
		DeviceId = deviceId;
	}

	/// <summary>
	/// Creates an <see cref="IOutputDevice"/> using the specified <c>DeviceId</c>. This method returns an instance of a lightweight
	/// <see cref="OutputDeviceWithId"/> that is not a fully-functional <see cref="OutputDevice"/>.
	/// </summary>
	/// <param name="deviceId">The device identifier for which an <see cref="OutputDeviceWithId"/> instance is created.</param>
	/// <returns>An instance of <see cref="OutputDeviceWithId"/> with the specified <c>DeviceId</c>.</returns>
	public static OutputDeviceWithId WithId(uint deviceId)
	{
		return new() { DeviceId = deviceId };
	}

	protected bool Frozen { get; private set; }
	protected bool Disposed { get; private set; }
	public uint DeviceId { get; }

	/// <summary>
	/// <c>DeviceId</c> of the input-side device this output also surfaces as, when one
	/// exists on this platform (vJoy device → DirectInput entry on Windows; uinput
	/// device → evdev event node on Linux). <c>null</c> when the output has no input
	/// counterpart or the backend couldn't determine the mapping. Assigned by the
	/// factory at <see cref="IOutputDeviceFactory.EnumerateConnectedOutputDevices"/> time.
	/// </summary>
	public int? InputDeviceId { get; internal set; }

	public void Freeze()
	{
		Frozen = true;
	}

	public abstract void SetAxisValue(Axis axis, double normalizedValue);
	public abstract void SetButtonState(int buttonNumber, bool pressed);

	public void Dispose()
	{
		if (Disposed)
		{
			return;
		}

		OnDispose();
		Disposed = true;
	}

	protected abstract void OnDispose();

	protected void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(Disposed, this);
	}

	protected void ThrowIfFrozen([CallerMemberName] string? memberName = null)
	{
		if (!Frozen)
		{
			return;
		}

		throw new InvalidOperationException($"Cannot modify {memberName} of vJoy device after it has been frozen.");
	}
}