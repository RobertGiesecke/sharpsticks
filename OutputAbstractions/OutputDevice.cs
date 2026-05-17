using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.OutputAbstractions;

public abstract class OutputDevice : IDisposable, IOutputDevice
{
	protected OutputDevice(uint deviceId)
	{
		DeviceId = deviceId;
	}

	protected bool Frozen { get; private set; }
	protected bool Disposed { get; private set; }
	public uint DeviceId { get; }

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