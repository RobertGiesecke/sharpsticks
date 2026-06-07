namespace SharpSticks.Tests;

/// <summary>
/// Validation of the testing-infrastructure fakes themselves — the things
/// every other test in this assembly leans on.
/// </summary>
public sealed class FakeDeviceTests
{
	// ── FakeDeviceManager ────────────────────────────────────────────────

	[Fact]
	public void Manager_AddInputDevice_AutoAssignsIds_Starting_AtOne()
	{
		using var fakes = new FakeDeviceManager();

		var a = fakes.AddInputDevice("A").AddAxis(Axis.X).Build();
		var b = fakes.AddInputDevice("B").AddAxis(Axis.X).Build();
		var c = fakes.AddInputDevice("C").AddAxis(Axis.X).Build();

		Assert.Equal(1, a.DeviceId);
		Assert.Equal(2, b.DeviceId);
		Assert.Equal(3, c.DeviceId);
	}

	[Fact]
	public void Manager_AddInputDevice_HonorsExplicitId_AndContinuesPastIt()
	{
		using var fakes = new FakeDeviceManager();

		var pinned = fakes.AddInputDevice("Pinned", deviceId: 7).AddAxis(Axis.X).Build();
		var next = fakes.AddInputDevice("Next").AddAxis(Axis.X).Build();

		Assert.Equal(7, pinned.DeviceId);
		Assert.Equal(8, next.DeviceId);
	}

	[Fact]
	public void Manager_AddOutputDevice_AutoAssignsIds_Starting_AtOne()
	{
		using var fakes = new FakeDeviceManager();

		var a = fakes.AddOutputDevice().AddAxis(Axis.X).Build();
		var b = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		Assert.Equal(1u, a.DeviceId);
		Assert.Equal(2u, b.DeviceId);
	}

	[Fact]
	public void Manager_InputDevices_ReflectsRegistrationOrder()
	{
		using var fakes = new FakeDeviceManager();

		var first = fakes.AddInputDevice("First").AddAxis(Axis.X).Build();
		var second = fakes.AddInputDevice("Second").AddAxis(Axis.X).Build();

		Assert.Equal(2, fakes.InputDevices.Length);
		Assert.Same(first, fakes.InputDevices[0]);
		Assert.Same(second, fakes.InputDevices[1]);
	}

	[Fact]
	public void Manager_OutputDeviceFactory_OpensRegisteredDevice()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		IOutputDeviceFactory factory = fakes.OutputDeviceFactory;
		using var opened = factory.EnumerateConnectedOutputDevices(
			[new(output.DeviceId, [], [], [])],
			[]);

		Assert.Same(output, opened[0]);
	}

	[Fact]
	public void Manager_OutputDeviceFactory_OpenUnknownId_Throws()
	{
		using var fakes = new FakeDeviceManager();
		fakes.AddOutputDevice().AddAxis(Axis.X).Build();   // id 1

		IOutputDeviceFactory factory = fakes.OutputDeviceFactory;
		Assert.Throws<InvalidOperationException>(
			() => factory.EnumerateConnectedOutputDevices([new(99, [], [], [])], []));
	}

	[Fact]
	public void Manager_Dispose_PreventsFurtherUse()
	{
		var fakes = new FakeDeviceManager();
		fakes.Dispose();

		Assert.Throws<ObjectDisposedException>(() => fakes.AddInputDevice("X"));
		Assert.Throws<ObjectDisposedException>(() => fakes.AddOutputDevice());
	}

	[Fact]
	public void Manager_Dispose_DisposesAllBuiltDevices()
	{
		var fakes = new FakeDeviceManager();
		var input = fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		fakes.Dispose();

		// Input device's DataAvailable handle is disposed → using it throws.
		Assert.Throws<ObjectDisposedException>(() => input.DataAvailable.WaitOne(0));

		// Output device rejects further writes with ObjectDisposedException
		// (thrown by OutputDevice.ThrowIfDisposed).
		Assert.Throws<ObjectDisposedException>(() => output.SetAxisValue(Axis.X, 0.5));
	}

	// ── FakeInputDeviceBuilder ──────────────────────────────────────────

	[Fact]
	public void InputBuilder_AddAxis_WithDuplicate_Throws()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddInputDevice("Stick").AddAxis(Axis.X);

		Assert.Throws<InvalidOperationException>(() => builder.AddAxis(Axis.X));
	}

	[Fact]
	public void InputBuilder_AddAxis_WithRestAt_SetsInitialReportedValue()
	{
		using var fakes = new FakeDeviceManager();
		var stick = fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X, restAt: -0.4)
			.Build();

		Assert.True(stick.TryReadState(out var state, out _));
		var sample = stick.ReadAxisDebugSample(state, stick.BindAxis(Axis.X));
		Assert.Equal(-0.4, sample.NormalizedValue, 1e-9);
	}

	[Fact]
	public void InputBuilder_AfterBuild_FurtherEditsThrow()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddInputDevice("Stick").AddAxis(Axis.X);
		builder.Build();

		Assert.Throws<InvalidOperationException>(() => builder.AddAxis(Axis.Y));
		Assert.Throws<InvalidOperationException>(() => builder.AddButtons(4));
	}

	[Fact]
	public void InputBuilder_Build_IsIdempotent()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddInputDevice("Stick").AddAxis(Axis.X);

		var a = builder.Build();
		var b = builder.Build();

		Assert.Same(a, b);
		Assert.Single(fakes.InputDevices);  // not registered twice
	}

	// ── FakeOutputDeviceBuilder ─────────────────────────────────────────

	[Fact]
	public void OutputBuilder_AddAxis_WithDuplicate_Throws()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddOutputDevice().AddAxis(Axis.X);

		Assert.Throws<InvalidOperationException>(() => builder.AddAxis(Axis.X));
	}

	[Fact]
	public void OutputBuilder_AfterBuild_FurtherEditsThrow()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddOutputDevice().AddAxis(Axis.X);
		builder.Build();

		Assert.Throws<InvalidOperationException>(() => builder.AddAxis(Axis.Y));
		Assert.Throws<InvalidOperationException>(() => builder.AddButtons(4));
	}

	[Fact]
	public void OutputBuilder_Build_IsIdempotent()
	{
		using var fakes = new FakeDeviceManager();
		var builder = fakes.AddOutputDevice().AddAxis(Axis.X);

		var a = builder.Build();
		var b = builder.Build();

		Assert.Same(a, b);
		Assert.Single(fakes.OutputDevices);
	}

	// ── FakeOutputDevice ────────────────────────────────────────────────

	[Fact]
	public void OutputDevice_StrictMode_RejectsUndeclaredAxis()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).AddButtons(4).Build();

		Assert.Throws<InvalidOperationException>(() => output.SetAxisValue(Axis.Y, 0.0));
	}

	[Fact]
	public void OutputDevice_StrictMode_RejectsButtonsBeyondDeclaredCount()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).AddButtons(2).Build();

		Assert.Throws<InvalidOperationException>(() => output.SetButtonState(buttonNumber: 5, pressed: true));
	}

	[Fact]
	public void OutputDevice_PermissiveMode_AcceptsAnyAxisOrButton()
	{
		// Direct construction (not via the manager builder) → declaredAxes=null → permissive.
		using var output = new FakeOutputDevice(deviceId: 1);

		output.SetAxisValue(Axis.X, 0.5);
		output.SetAxisValue(Axis.Slider2, -0.7);
		output.SetButtonState(buttonNumber: 999, pressed: true);

		Assert.Equal(0.5, output.GetAxisValue(Axis.X), 1e-9);
		Assert.Equal(-0.7, output.GetAxisValue(Axis.Slider2), 1e-9);
		Assert.True(output.GetButtonState(999));
	}

	[Fact]
	public void OutputDevice_SetAxisValue_ClampsToSignedRange()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		output.SetAxisValue(Axis.X, 1.5);
		Assert.Equal(1.0, output.GetAxisValue(Axis.X), 1e-9);

		output.SetAxisValue(Axis.X, -2.0);
		Assert.Equal(-1.0, output.GetAxisValue(Axis.X), 1e-9);
	}

	[Fact]
	public void OutputDevice_AfterDispose_WritesThrow()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		output.Dispose();

		Assert.Throws<ObjectDisposedException>(() => output.SetAxisValue(Axis.X, 0.5));
	}

	[Fact]
	public void OutputDevice_Frozen_RejectsFurtherWrites()
	{
		using var fakes = new FakeDeviceManager();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		output.Freeze();

		Assert.Throws<InvalidOperationException>(() => output.SetAxisValue(Axis.X, 0.5));
	}

	// ── FakeOutputDeviceFactory ─────────────────────────────────────────

	[Fact]
	public void Factory_GetUnregistered_Throws()
	{
		var factory = new FakeOutputDeviceFactory();

		Assert.Throws<InvalidOperationException>(() => factory.Get(7));
	}

	[Fact]
	public void Factory_RegisterDuplicate_Throws()
	{
		var factory = new FakeOutputDeviceFactory();
		var first = new FakeOutputDevice(deviceId: 3);
		var dup = new FakeOutputDevice(deviceId: 3);

		factory.Register(first);

		Assert.Throws<InvalidOperationException>(() => factory.Register(dup));
	}

	// ── FakeJoystickDevice ──────────────────────────────────────────────

	[Fact]
	public void InputDevice_TryReadState_ReturnsButtonBitsInLowAndHigh()
	{
		using var fakes = new FakeDeviceManager();
		var stick = fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(80).Build();

		stick.PressButton(1);     // low bit 0
		stick.PressButton(64);    // low bit 63
		stick.PressButton(65);    // high bit 0
		stick.PressButton(80);    // high bit 15

		Assert.True(stick.TryReadState(out var state, out _));
		Assert.True(state.IsButtonPressed(1));
		Assert.True(state.IsButtonPressed(64));
		Assert.True(state.IsButtonPressed(65));
		Assert.True(state.IsButtonPressed(80));
		Assert.False(state.IsButtonPressed(2));
	}

	[Fact]
	public void InputDevice_SetButton_OutOfRange_Throws()
	{
		using var fakes = new FakeDeviceManager();
		var stick = fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();

		Assert.Throws<ArgumentOutOfRangeException>(() => stick.PressButton(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => stick.PressButton(5));
	}

	[Fact]
	public void InputDevice_ReadAxisDebugSample_AppliesAxisModeClampAndInvert()
	{
		using var fakes = new FakeDeviceManager();
		var stick = fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();

		stick.SetAxisValue(Axis.X, 0.7);
		Assert.True(stick.TryReadState(out var state, out _));

		// Signed (default) — value as-is in [-1, 1].
		var signed = stick.ReadAxisDebugSample(state, stick.BindAxis(Axis.X));
		Assert.Equal(0.7, signed.NormalizedValue, 1e-9);

		// Unsigned — value clamped to [0, 1].
		var unsigned = stick.ReadAxisDebugSample(
			state, stick.BindAxis(Axis.X) with { Mode = AxisMode.Unsigned });
		Assert.Equal(0.7, unsigned.NormalizedValue, 1e-9);

		// Invert (signed) flips sign.
		var inverted = stick.ReadAxisDebugSample(
			state, stick.BindAxis(Axis.X) with { Invert = true });
		Assert.Equal(-0.7, inverted.NormalizedValue, 1e-9);
	}

	[Fact]
	public void InputDevice_SignalsDataAvailable_OnAxisOrButtonChange()
	{
		using var fakes = new FakeDeviceManager();
		var stick = fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();

		Assert.False(stick.DataAvailable.WaitOne(0));   // unsignalled initially

		stick.SetAxisValue(Axis.X, 0.5);
		Assert.True(stick.DataAvailable.WaitOne(0));    // signalled, AutoReset clears

		Assert.False(stick.DataAvailable.WaitOne(0));   // back to unsignalled

		stick.PressButton(1);
		Assert.True(stick.DataAvailable.WaitOne(0));
	}

	// ── IFakeDevice contract ────────────────────────────────────────────

	[Fact]
	public void IFakeDevice_BothFakesImplementContract()
	{
		using var fakes = new FakeDeviceManager();
		IFakeDevice input = fakes.AddInputDevice("In").AddAxis(Axis.X).AddButtons(2).Build();
		IFakeDevice output = fakes.AddOutputDevice().AddAxis(Axis.X).AddButtons(2).Build();

		input.SetAxisValue(Axis.X, 0.3);
		input.SetButtonState(1, pressed: true);
		Assert.Equal(0.3, input.GetAxisValue(Axis.X), 1e-9);
		Assert.True(input.GetButtonState(1));

		output.SetAxisValue(Axis.X, -0.4);
		output.SetButtonState(2, pressed: true);
		Assert.Equal(-0.4, output.GetAxisValue(Axis.X), 1e-9);
		Assert.True(output.GetButtonState(2));
	}
}
