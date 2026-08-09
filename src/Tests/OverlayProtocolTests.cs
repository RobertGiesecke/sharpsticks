using System.Buffers.Binary;
using System.Text;
using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// Pins the binary layout the joyviz overlay consumes: the descriptor frame
/// (kind / axes / buttons / name per device, in order) and the state frame
/// (int16 axes at round(v·32767), buttons bit-packed LSB-first with button
/// (i+1) on bit i). Devices are flagged as output purely by the "vJoy" name
/// prefix.
/// </summary>
public sealed class OverlayProtocolTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void Descriptor_EncodesKindAxesButtonsAndName_PerDeviceInOrder()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Ry).AddButtons(3).Build();
		var mirror = _Fakes.AddInputDevice("vJoy Device")
			.AddAxis(Axis.Z).AddButtons(9).Build();

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick, mirror], version: 7);
		var d = protocol.Descriptor;

		Assert.Equal((byte)0x01, d[0]);
		Assert.Equal((byte)7, d[1]);
		Assert.Equal((byte)2, d[2]);

		var pos = 3;
		// Stick: input kind, 2 axes, 3 buttons, name, axis enum bytes.
		Assert.Equal((byte)0, d[pos++]);
		Assert.Equal((byte)2, d[pos++]);
		Assert.Equal((byte)3, d[pos++]);
		var nameLen = d[pos++];
		Assert.Equal("Stick", Encoding.UTF8.GetString(d, pos, nameLen));
		pos += nameLen;
		Assert.Equal((byte)Axis.X, d[pos++]);
		Assert.Equal((byte)Axis.Ry, d[pos++]);

		// The vJoy mirror is flagged as an output device by its name.
		Assert.Equal((byte)1, d[pos++]);
		Assert.Equal((byte)1, d[pos++]);
		Assert.Equal((byte)9, d[pos++]);
		nameLen = d[pos++];
		Assert.Equal("vJoy Device", Encoding.UTF8.GetString(d, pos, nameLen));
		pos += nameLen;
		Assert.Equal((byte)Axis.Z, d[pos++]);

		Assert.Equal(d.Length, pos);
	}

	[Fact]
	public void WriteState_EncodesAxesAsInt16_AndButtonsLsbFirst()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Y).AddButtons(9).Build();
		stick.SetAxisValue(Axis.X, 0.5);
		stick.SetAxisValue(Axis.Y, -1.0);
		stick.PressButton(1);
		stick.PressButton(9);
		Assert.True(stick.TryReadState(out var state, out _));

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var frame = protocol.WriteState([state]).ToArray();

		// [0x02][ver][X int16][Y int16][2 button bytes for 9 buttons]
		Assert.Equal(8, frame.Length);
		Assert.Equal((byte)0x02, frame[0]);
		Assert.Equal((byte)1, frame[1]);
		Assert.Equal((short)16384, BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(2)));
		Assert.Equal((short)-32767, BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(4)));
		// Button (i+1) lands on bit i: button 1 → byte 0 bit 0, button 9 → byte 1 bit 0.
		Assert.Equal((byte)0b1, frame[6]);
		Assert.Equal((byte)0b1, frame[7]);
	}

	[Fact]
	public void WriteState_NullState_SerializesCenteredAxesAndReleasedButtons()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddButtons(2).Build();
		stick.SetAxisValue(Axis.X, 1.0);
		stick.PressButton(1);

		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		var frame = protocol.WriteState([null]).ToArray();

		Assert.Equal(5, frame.Length);
		Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(2)));
		Assert.Equal((byte)0, frame[4]);
	}

	[Fact]
	public void WriteState_ClearsStaleBits_WhenReusingTheBuffer()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddButtons(2).Build();

		stick.PressButton(2);
		Assert.True(stick.TryReadState(out var pressed, out _));
		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick], version: 1);
		Assert.Equal((byte)0b10, protocol.WriteState([pressed]).ToArray()[4]);

		// The state buffer is reused frame-to-frame; a released button must not
		// leave its old bit behind.
		stick.ReleaseButton(2);
		Assert.True(stick.TryReadState(out var released, out _));
		Assert.Equal((byte)0, protocol.WriteState([released]).ToArray()[4]);
	}
}
