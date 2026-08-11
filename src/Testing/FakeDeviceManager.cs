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
	private FakeInputDeviceFactory? _InputDeviceFactory;
	private readonly List<FakeOutputDevice> _OutputDevicesList = [];
	private int _NextInputDeviceId = 1;
	private uint _NextOutputDeviceId = 1;
	private bool _Disposed;

	public IOutputDeviceFactory<FakeOutputDevice> OutputDeviceFactory => _OutputDeviceFactory;

	/// <summary>
	/// Enumerates this manager's registered input devices, for code paths that
	/// take a factory instead of a device list (e.g. the overlay serve path).
	/// </summary>
	public IJoystickDeviceFactory<FakeJoystickDevice> InputDeviceFactory =>
		_InputDeviceFactory ??= new(this);

	public ImmutableArray<FakeJoystickDevice> InputDevices => [.._InputDevices];

	public IReadOnlyList<FakeOutputDevice> OutputDevices => _OutputDevicesList;

	/// <summary>
	/// Virtual clock handed to every runtime built via <see cref="BuildRuntime"/>.
	/// Advance it with <see cref="FakeTimeSource.Advance"/> when a test exercises
	/// time-dependent behavior.
	/// </summary>
	public FakeTimeSource TimeSource { get; } = new();

	/// <summary>
	/// Builds a runtime on this manager's fakes: unless the options say
	/// otherwise, wires in <see cref="OutputDeviceFactory"/> and
	/// <see cref="TimeSource"/>. Tests should build through this instead of
	/// <c>Runtime.Build</c> so they run on virtual time by default — a modifier
	/// that later grows time-dependent behavior then fails deterministically
	/// (virtual time stands still until advanced) instead of silently running
	/// on the wall clock.
	/// </summary>
	public IFakesOutputRuntimeContext BuildRuntime(
		RuntimeBuilder.BuildOptions<FakeJoystickDevice, FakeOutputDevice> options)
	{
		ThrowIfDisposed();
		return FakesRuntime.Build(options with
		{
			OutputDeviceFactory = options.OutputDeviceFactory ?? OutputDeviceFactory,
			TimeSource = options.TimeSource ?? TimeSource,
		});
	}

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
