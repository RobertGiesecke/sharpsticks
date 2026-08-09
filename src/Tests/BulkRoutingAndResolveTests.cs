namespace SharpSticks.Tests;

/// <summary>
/// The bulk helpers scripts lean on: <c>RouteButtonsToOutput</c> /
/// <c>RouteAxesToOutput</c> expanding to one route per button/axis at build
/// (with predicate filtering), and <c>ResolveDevice</c>'s exact → partial →
/// ambiguous/missing name resolution.
/// </summary>
public sealed class BulkRoutingAndResolveTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void RouteButtonsAndAxesToOutput_MirrorTheWholeDevice()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(4).Build();
		var output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(4).Build();

		using var runtime = _Fakes.BuildRuntime(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			Routes =
			[
				stick.RouteButtonsToOutput(output.DeviceId),
				stick.RouteAxesToOutput(output.DeviceId),
			],
		});

		stick.SetAxisValue(Axis.X, 0.4);
		stick.SetAxisValue(Axis.Y, -0.6);
		stick.PressButton(1);
		stick.PressButton(3);
		runtime.ProcessWithDefaultFrameTime();

		Assert.Equal(0.4, output.GetAxisValue(Axis.X), Precision);
		Assert.Equal(-0.6, output.GetAxisValue(Axis.Y), Precision);
		Assert.True(output.GetButtonState(1));
		Assert.False(output.GetButtonState(2));
		Assert.True(output.GetButtonState(3));
	}

	[Fact]
	public void RouteButtonsToOutput_Predicate_FiltersTheExpandedRoutes()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		var output = _Fakes.AddOutputDevice().AddButtons(4).Build();

		using var runtime = _Fakes.BuildRuntime(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			Routes =
			[
				stick.RouteButtonsToOutput(output.DeviceId,
					(_, binding) => binding.ButtonNumber % 2 == 0),
			],
		});

		stick.PressButton(1);
		stick.PressButton(2);
		runtime.ProcessWithDefaultFrameTime();

		Assert.False(output.GetButtonState(1)); // filtered out
		Assert.True(output.GetButtonState(2));
	}

	[Fact]
	public void ResolveDevice_PrefersExactName_ThenUniquePartial()
	{
		var left = _Fakes.AddInputDevice("Left Stick").Build();
		var pedals = _Fakes.AddInputDevice("Rudder Pedals").Build();
		_Fakes.AddInputDevice("Right Stick").Build();
		var devices = _Fakes.InputDevices;

		Assert.Same(left, devices.ResolveDevice("left stick"));   // exact, case-insensitive
		Assert.Same(pedals, devices.ResolveDevice("Rudder"));     // unique partial
	}

	[Fact]
	public void ResolveDevice_Throws_OnAmbiguousMissingOrEmptyNames()
	{
		_Fakes.AddInputDevice("Left Stick").Build();
		_Fakes.AddInputDevice("Right Stick").Build();
		var devices = _Fakes.InputDevices;

		Assert.Contains("partially match", Assert.Throws<InvalidOperationException>(
			() => devices.ResolveDevice("Stick")).Message);
		Assert.Contains("No joystick device", Assert.Throws<InvalidOperationException>(
			() => devices.ResolveDevice("Throttle")).Message);
		Assert.Throws<InvalidOperationException>(() => devices.ResolveDevice(" "));
	}

	[Fact]
	public void CollectDevices_SelectsByIds_AndThrowsForUnknownOnes()
	{
		var left = _Fakes.AddInputDevice("Left Stick", deviceId: 3).Build();
		var right = _Fakes.AddInputDevice("Right Stick", deviceId: 5).Build();
		var devices = _Fakes.InputDevices;

		var selected = new Dictionary<int, FakeJoystickDevice>();
		devices.CollectDevices([3, 5, 3], selected);
		Assert.Equal(2, selected.Count);
		Assert.Same(left, selected[3]);
		Assert.Same(right, selected[5]);

		Assert.Contains("not available", Assert.Throws<InvalidOperationException>(
			() => devices.CollectDevices([4], new Dictionary<int, FakeJoystickDevice>())).Message);
	}
}
