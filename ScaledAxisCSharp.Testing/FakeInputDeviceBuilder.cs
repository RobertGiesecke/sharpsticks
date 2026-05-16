namespace ScaledAxisCSharp.Testing;

/// <summary>
/// Fluent builder for a <see cref="FakeJoystickDevice"/>. Returned by
/// <see cref="FakeDeviceManager.AddInputDevice"/>; configure axes/buttons and
/// call <see cref="Build"/> (or rely on the implicit conversion) to materialize
/// the device. The built device is registered with the manager for disposal.
/// </summary>
public sealed class FakeInputDeviceBuilder
{
	private readonly FakeDeviceManager _Manager;
	private readonly int _DeviceId;
	private readonly string _Name;
	private readonly List<Axis> _Axes = [];
	private readonly Dictionary<Axis, double> _RestValues = [];
	private int _ButtonCount;
	private string? _InstanceName;
	private FakeJoystickDevice? _Built;

	internal FakeInputDeviceBuilder(FakeDeviceManager manager, int deviceId, string name)
	{
		_Manager = manager;
		_DeviceId = deviceId;
		_Name = name;
	}

	public FakeInputDeviceBuilder AddAxis(Axis axis, double restAt = 0.0)
	{
		ThrowIfBuilt();
		if (_Axes.Contains(axis))
		{
			throw new InvalidOperationException($"Axis '{axis}' is already declared on this device.");
		}

		_Axes.Add(axis);
		_RestValues[axis] = restAt;
		return this;
	}

	public FakeInputDeviceBuilder AddButtons(int count)
	{
		ThrowIfBuilt();
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count));
		}

		_ButtonCount = count;
		return this;
	}

	public FakeInputDeviceBuilder WithInstanceName(string instanceName)
	{
		ThrowIfBuilt();
		_InstanceName = instanceName;
		return this;
	}

	public FakeJoystickDevice Build()
	{
		if (_Built is not null)
		{
			return _Built;
		}

		var device = new FakeJoystickDevice(
			_DeviceId,
			_Name,
			[.._Axes],
			Math.Max(_ButtonCount, 1),
			_InstanceName);

		foreach (var (axis, rest) in _RestValues)
		{
			if (rest != 0.0)
			{
				device.SetAxisValue(axis, rest);
			}
		}

		_Built = device;
		_Manager.RegisterInput(device);
		return device;
	}

	public static implicit operator FakeJoystickDevice(FakeInputDeviceBuilder builder) =>
		builder.Build();

	private void ThrowIfBuilt()
	{
		if (_Built is not null)
		{
			throw new InvalidOperationException("Device has already been built; configuration is frozen.");
		}
	}
}
