namespace ScaledAxisCSharp.Testing;

/// <summary>
/// Fluent builder for a <see cref="FakeOutputDevice"/>. Returned by
/// <see cref="FakeDeviceManager.AddOutputDevice"/>; configure axes/buttons and
/// call <see cref="Build"/> (or rely on the implicit conversion) to materialize
/// and register the device with the manager's output factory.
/// </summary>
public sealed class FakeOutputDeviceBuilder
{
	private readonly FakeDeviceManager _Manager;
	private readonly HashSet<Axis> _Axes = [];
	private int? _ButtonCount;
	private FakeOutputDevice? _Built;

	internal FakeOutputDeviceBuilder(FakeDeviceManager manager)
	{
		_Manager = manager;
	}

	public FakeOutputDeviceBuilder AddAxis(Axis axis)
	{
		ThrowIfBuilt();
		if (!_Axes.Add(axis))
		{
			throw new InvalidOperationException($"Axis '{axis}' is already declared on this device.");
		}

		return this;
	}

	public FakeOutputDeviceBuilder AddButtons(int count)
	{
		ThrowIfBuilt();
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count));
		}

		_ButtonCount = count;
		return this;
	}

	public FakeOutputDevice Build()
	{
		if (_Built is not null)
		{
			return _Built;
		}

		var device = _Manager.BuildOutput(this, _Axes, _ButtonCount);
		_Built = device;
		return device;
	}

	public static implicit operator FakeOutputDevice(FakeOutputDeviceBuilder builder) =>
		builder.Build();

	private void ThrowIfBuilt()
	{
		if (_Built is not null)
		{
			throw new InvalidOperationException("Device has already been built; configuration is frozen.");
		}
	}
}
