using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// Round-trips the overlay's binary frames through <see cref="OverlayFrameReader"/>
/// and asserts on the deserialized form. One golden byte-level test pins the
/// raw grammar itself — that is the actual contract with the joyviz client;
/// everything else stays readable as scenarios.
/// </summary>
public sealed class OverlayProtocolTests : IDisposable
{
	// One int16 step of quantization noise.
	private const double Quantum = 1.0 / 32767.0;

	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Descriptor_RoundTrips_KindAxesButtonsAndName_PerDeviceInOrder()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Ry).AddButtons(3).Build();
		var mirror = _Fakes.AddInputDevice("vJoy Device")
			.AddAxis(Axis.Z).AddButtons(9).Build();

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick, mirror], version: 7);
		var descriptor = OverlayFrameReader.ReadDescriptor(protocol.Descriptor);

		Assert.Equal(7, descriptor.Version);
		Assert.Equal(2, descriptor.Devices.Count);

		var first = descriptor.Devices[0];
		Assert.False(first.IsOutput);
		Assert.Equal("Stick", first.Name);
		Assert.Equal([Axis.X, Axis.Ry], first.Axes);
		Assert.Equal(3, first.ButtonCount);

		// The vJoy mirror is flagged as an output device by its name.
		var second = descriptor.Devices[1];
		Assert.True(second.IsOutput);
		Assert.Equal("vJoy Device", second.Name);
		Assert.Equal([Axis.Z], second.Axes);
		Assert.Equal(9, second.ButtonCount);
	}

	[Fact]
	public void State_RoundTrips_AxisValuesAndButtons_AcrossByteBoundaries()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(9).Build();
		stick.SetAxisValue(Axis.X, 0.5);
		stick.SetAxisValue(Axis.Y, -1.0);
		stick.PressButton(1);
		stick.PressButton(9); // second button byte
		Assert.True(stick.TryReadState(out var state, out _));

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var descriptor = OverlayFrameReader.ReadDescriptor(protocol.Descriptor);
		var decoded = OverlayFrameReader.ReadState(protocol.WriteState([state]), descriptor);

		var device = Assert.Single(decoded.Devices);
		Assert.Equal(0.5, device.Axes[0], Quantum);
		Assert.Equal(-1.0, device.Axes[1], Quantum);
		Assert.Equal(
			[true, false, false, false, false, false, false, false, true],
			device.Buttons);
	}

	[Fact]
	public void State_NullDeviceState_DecodesAsCenteredAndReleased()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddButtons(2).Build();
		stick.SetAxisValue(Axis.X, 1.0);
		stick.PressButton(1);

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var descriptor = OverlayFrameReader.ReadDescriptor(protocol.Descriptor);
		var decoded = OverlayFrameReader.ReadState(protocol.WriteState([null]), descriptor);

		var device = Assert.Single(decoded.Devices);
		Assert.Equal([0.0], device.Axes);
		Assert.Equal([false, false], device.Buttons);
	}

	[Fact]
	public void State_ClearsStaleBits_WhenReusingTheBuffer()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddButtons(2).Build();
		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var descriptor = OverlayFrameReader.ReadDescriptor(protocol.Descriptor);

		stick.PressButton(2);
		Assert.True(stick.TryReadState(out var pressed, out _));
		Assert.True(OverlayFrameReader.ReadState(protocol.WriteState([pressed]), descriptor)
			.Devices[0].Buttons[1]);

		// The state buffer is reused frame-to-frame; a released button must not
		// leave its old bit behind.
		stick.ReleaseButton(2);
		Assert.True(stick.TryReadState(out var released, out _));
		Assert.False(OverlayFrameReader.ReadState(protocol.WriteState([released]), descriptor)
			.Devices[0].Buttons[1]);
	}

	[Fact]
	public void WireGrammar_GoldenBytes()
	{
		// The raw byte layout IS the contract with the joyviz client — pin it
		// once, literally: [0x01][ver][count] [kind][axes][buttons][nameLen]"Go"[axis]
		// and [0x02][ver][X int16 LE][one button byte].
		var stick = _Fakes.AddInputDevice("Go").AddAxis(Axis.X).AddButtons(2).Build();
		stick.SetAxisValue(Axis.X, 1.0);
		stick.PressButton(1);
		Assert.True(stick.TryReadState(out var state, out _));

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);

		Assert.Equal(
			(byte[])[0x01, 1, 1, 0, 1, 2, 2, (byte)'G', (byte)'o', (byte)Axis.X],
			protocol.Descriptor);
		Assert.Equal(
			(byte[])[0x02, 1, 0xFF, 0x7F, 0b1],
			protocol.WriteState([state]).ToArray());
	}
}
