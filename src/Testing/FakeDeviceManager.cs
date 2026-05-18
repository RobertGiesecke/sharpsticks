namespace SharpSticks.Testing;

/// <summary>
/// Disposable owner of every fake input / output device a test creates.
/// Provides the fluent <see cref="AddInputDevice"/> / <see cref="AddOutputDevice"/>
/// builders, exposes the input list ready to drop into
/// <c>RuntimeBuilder.BuildOptions.ConnectedDevices</c>, and a strict
/// <see cref="OutputDeviceFactory"/> for the runtime to open against.
/// </summary>
public sealed class FakeDeviceManager : IDisposable
{
	private readonly List<FakeJoystickDevice> _InputDevices = [];
	private readonly FakeOutputDeviceFactory _OutputDeviceFactory = new();
	private readonly List<FakeOutputDevice> _OutputDevicesList = [];
	private int _NextInputDeviceId = 1;
	private uint _NextOutputDeviceId = 1;
	private bool _Disposed;

	public IOutputDeviceFactory OutputDeviceFactory => _OutputDeviceFactory;

	public ImmutableArray<JoystickDevice> InputDevices => [.._InputDevices];

	public IReadOnlyList<FakeOutputDevice> OutputDevices => _OutputDevicesList;

	public FakeInputDeviceBuilder AddInputDevice(string name, int? deviceId = null)
	{
		ThrowIfDisposed();
		var id = deviceId ?? _NextInputDeviceId++;
		return new(this, id, name);
	}

	public FakeOutputDeviceBuilder AddOutputDevice()
	{
		ThrowIfDisposed();
		return new(this);
	}

	public FakeOutputDevice GetOutputDevice(uint deviceId) => _OutputDeviceFactory.Get(deviceId);

	internal void RegisterInput(FakeJoystickDevice device)
	{
		_InputDevices.Add(device);
		if (device.DeviceId >= _NextInputDeviceId)
		{
			_NextInputDeviceId = device.DeviceId + 1;
		}
	}

	internal void RegisterOutput(FakeOutputDevice device)
	{
		_OutputDeviceFactory.Register(device);
		_OutputDevicesList.Add(device);
	}

	internal FakeOutputDevice BuildOutput(
		FakeOutputDeviceBuilder deviceBuilder,
		IReadOnlyCollection<Axis> axes,
		int? buttonCount)
	{
		var device = new FakeOutputDevice(
			_NextOutputDeviceId++,
			declaredAxes: [..axes],
			buttonCount: buttonCount);

		_OutputDeviceFactory.Register(device);
		_OutputDevicesList.Add(device);
		return device;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_Disposed, this);

	public void Dispose()
	{
		if (_Disposed)
		{
			return;
		}

		_Disposed = true;
		foreach (var device in _InputDevices)
		{
			device.Dispose();
		}

		foreach (var device in _OutputDevicesList)
		{
			device.Dispose();
		}
	}
}
