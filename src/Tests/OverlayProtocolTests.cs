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
		var descriptor = OverlayFrames.ReadDescriptor(protocol.Descriptor);

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
	public void Descriptor_RoundTrips_KindAxesButtonsAndName_PerDeviceInOrder2()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Ry).AddButtons(3).Build();
		const string vjoyDeviceName = "vJoy Device";
		var mirror = _Fakes.AddInputDevice(vjoyDeviceName)
			.AddAxis(Axis.Z).AddButtons(9).Build();

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick, mirror], version: 7);
		var reader2 = OverlayFrames.CreateReader(protocol.Descriptor);

		Assert.Equal(FrameType.FrameDescriptor, reader2.GetFrameType().Value);
		Assert.Equal<byte>(7, reader2.GetVersion().Value);
		var readerOption = reader2.GetDescriptorFrameReader();
		Assert.True(readerOption.Success);
		var reader = readerOption.Value;

		Assert.Equal<byte>(2, reader.GetDeviceCount().Value);
		var deviceFrameReaderOption = reader.GetDeviceFrameReader();
		Assert.True(deviceFrameReaderOption.Success);
		scoped var deviceFrameReader = deviceFrameReaderOption.Value;

		Assert.True(deviceFrameReader.MoveNext(out scoped var first));

		Assert.False(first.IsOutput);
		Span<char> nameCharBuffer = stackalloc char[vjoyDeviceName.Length];

		Assert.Equal("Stick", first.GetNameSpan(nameCharBuffer));
		Assert.Equal(Axis.X, first.GetAxis(0));
		Assert.Equal(Axis.Ry, first.GetAxis(1));
		Assert.Equal<byte>(3, first.ButtonCount);

		// The vJoy mirror is flagged as an output device by its name.
		Assert.True(deviceFrameReader.MoveNext(out scoped var second));

		Assert.True(second.IsOutput);
		Assert.Equal(vjoyDeviceName, second.GetNameSpan(nameCharBuffer));
		Assert.Equal(Axis.Z, second.GetAxis(0));
		Assert.Equal<byte>(9, second.ButtonCount);
		Assert.False(deviceFrameReader.MoveNext(out _));

		using var names = new PooledList<string>(2); 
		foreach(var d in reader.EnumerateDevices())
		{
			names.Add(d.GetName());	
		}
		Assert.Equal(["Stick", vjoyDeviceName], names);
	}

	[Fact]
	public void State_StreamingReader_RoundTrips_PerDeviceValues()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(9).Build();
		var mirror = _Fakes.AddInputDevice("vJoy Device")
			.AddAxis(Axis.Z).AddButtons(2).Build();
		stick.SetAxisValue(Axis.X, 0.5);
		stick.SetAxisValue(Axis.Y, -1.0);
		stick.PressButton(1);
		stick.PressButton(9);
		mirror.SetAxisValue(Axis.Z, 1.0);
		mirror.PressButton(2);
		Assert.True(stick.TryReadState(out var stickState, out _));
		Assert.True(mirror.TryReadState(out var mirrorState, out _));

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick, mirror], version: 1);
		var frame = protocol.WriteState([stickState, mirrorState]);

		var stateOption = OverlayFrames.CreateReader(frame).GetStateFrameReader();
		Assert.True(stateOption.Success);
		Assert.Equal<byte>(1, stateOption.Value.GetVersion().Value);
		var deviceStates = stateOption.Value.GetDeviceStateReader();

		// The state frame carries no shapes — co-iterate the descriptor.
		var infos = OverlayFrames.CreateReader(protocol.Descriptor)
			.GetDescriptorFrameReader().Value
			.GetDeviceFrameReader().Value;

		Assert.True(infos.MoveNext(out var stickInfo));
		Assert.True(deviceStates.MoveNext(in stickInfo, out var stickDecoded));
		Assert.Equal(2, stickDecoded.AxisCount);
		Assert.Equal(0.5, stickDecoded.GetAxisValue(0), Quantum);
		Assert.Equal(-1.0, stickDecoded.GetAxisValue(1), Quantum);
		Assert.True(stickDecoded.IsButtonPressed(1));
		Assert.False(stickDecoded.IsButtonPressed(2));
		Assert.True(stickDecoded.IsButtonPressed(9));
		Assert.False(stickDecoded.IsButtonPressed(10)); // padding bit
		Assert.False(stickDecoded.IsButtonPressed(16)); // padding bit

		Assert.True(infos.MoveNext(out var mirrorInfo));
		Assert.True(deviceStates.MoveNext(in mirrorInfo, out var mirrorDecoded));
		Assert.Equal(1.0, mirrorDecoded.GetAxisValue(0), Quantum);
		Assert.False(mirrorDecoded.IsButtonPressed(1));
		Assert.True(mirrorDecoded.IsButtonPressed(2));

		// Exactly consumed — the streaming equivalent of the grammar check.
		Assert.Equal(0, deviceStates.RemainingBytes);
		Assert.False(deviceStates.MoveNext(in mirrorInfo, out _));
	}

	[Fact]
	public void StreamingReaders_RejectTheWrongFrameKind()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2).Build();
		Assert.True(stick.TryReadState(out var state, out _));
		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var stateFrame = protocol.WriteState([state]);

		Assert.False(OverlayFrames.CreateReader(protocol.Descriptor).GetStateFrameReader().Success);
		Assert.False(OverlayFrames.CreateReader(stateFrame).GetDescriptorFrameReader().Success);
	}

	[Fact]
	public void State_StreamingReader_StopsCleanly_OnTruncatedFrames()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(9).Build();
		Assert.True(stick.TryReadState(out var state, out _));
		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var truncated = protocol.WriteState([state])[..^3]; // cut into the device's data

		var deviceStates = OverlayFrames.CreateReader(truncated)
			.GetStateFrameReader().Value.GetDeviceStateReader();

		Assert.False(deviceStates.MoveNext(axisCount: 2, buttonCount: 9, out _));
		// The unconsumed remainder is how a caller tells truncation from completion.
		Assert.True(deviceStates.RemainingBytes > 0);
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
		var descriptor = OverlayFrames.ReadDescriptor(protocol.Descriptor);
		var decoded = OverlayFrames.ReadState(protocol.WriteState([state]), descriptor);

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
		var descriptor = OverlayFrames.ReadDescriptor(protocol.Descriptor);
		var decoded = OverlayFrames.ReadState(protocol.WriteState([null]), descriptor);

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
		var descriptor = OverlayFrames.ReadDescriptor(protocol.Descriptor);

		stick.PressButton(2);
		Assert.True(stick.TryReadState(out var pressed, out _));
		Assert.True(OverlayFrames.ReadState(protocol.WriteState([pressed]), descriptor)
			.Devices[0].Buttons[1]);

		// The state buffer is reused frame-to-frame; a released button must not
		// leave its old bit behind.
		stick.ReleaseButton(2);
		Assert.True(stick.TryReadState(out var released, out _));
		Assert.False(OverlayFrames.ReadState(protocol.WriteState([released]), descriptor)
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