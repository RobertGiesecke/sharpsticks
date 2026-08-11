namespace SharpSticks.Tests;

/// <summary>
/// The standalone overlay server must not open the input-side mirrors of virtual
/// output devices — holding vJoy's DirectInput entry open blocks the routing
/// engine from feeding it. These tests pin the serve path's device selection:
/// input-only by default, mirrors opt-in via <c>IncludeOutputDevices</c>.
/// </summary>
public sealed class OverlayServeDeviceFilterTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	private FakeJoystickDevice AddStick() =>
		_Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();

	private FakeJoystickDevice AddVJoyMirror() =>
		_Fakes.AddInputDevice("vJoy Device")
			.AddAxis(Axis.X)
			.AddButtons(8)
			.WithProductGuid(VirtualOutputProducts.VJoyProductGuid)
			.Build();

	[Fact]
	public void ByDefault_VirtualOutputMirrors_AreNotServed()
	{
		var stick = AddStick();
		AddVJoyMirror();

		var (fail, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory, new ServeOptions<FakeJoystickDevice>());

		Assert.Null(fail);
		var device = Assert.Single(runtimeOptions!.Value.Devices);
		Assert.Same(stick, device);
	}

	[Fact]
	public void NameOnlyVJoyEntry_WithoutProductGuid_IsAlsoSkipped()
	{
		var stick = AddStick();
		_Fakes.AddInputDevice("vJoy Device").AddAxis(Axis.X).Build();

		var (_, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory, new ServeOptions<FakeJoystickDevice>());

		var device = Assert.Single(runtimeOptions!.Value.Devices);
		Assert.Same(stick, device);
	}

	[Fact]
	public void IncludeOutputDevices_ServesTheMirrorsToo()
	{
		AddStick();
		AddVJoyMirror();

		var (fail, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory,
			new ServeOptions<FakeJoystickDevice> { IncludeOutputDevices = true });

		Assert.Null(fail);
		Assert.Equal(2, runtimeOptions!.Value.Devices.Length);
	}

	[Fact]
	public void DevicePredicate_CannotReintroduceMirrors_WithoutIncludeOutputDevices()
	{
		AddStick();
		AddVJoyMirror();

		var (_, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory,
			new ServeOptions<FakeJoystickDevice>
			{
				// Explicitly matches everything, including the mirror.
				DevicePredicate = _ => true,
			});

		var device = Assert.Single(runtimeOptions!.Value.Devices);
		Assert.Equal("Stick", device.Name);
	}

	[Fact]
	public void DevicePredicate_StillFilters_WhenOutputsAreIncluded()
	{
		AddStick();
		var mirror = AddVJoyMirror();

		var (_, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory,
			new ServeOptions<FakeJoystickDevice>
			{
				IncludeOutputDevices = true,
				DevicePredicate = device => device.Name.StartsWith("vJoy", StringComparison.Ordinal),
			});

		var device = Assert.Single(runtimeOptions!.Value.Devices);
		Assert.Same(mirror, device);
	}

	[Fact]
	public void OnlyMirrorsPresent_FailsWithNoInputDevices()
	{
		AddVJoyMirror();

		var (fail, runtimeOptions) = OverlayServer.Default.InferRuntimeOptions(
			_Fakes.InputDeviceFactory, new ServeOptions<FakeJoystickDevice>());

		Assert.Null(runtimeOptions);
		Assert.Equal(OverlayServeFailReason.NoInputDevices, fail!.Value.FailReason);
	}
}
