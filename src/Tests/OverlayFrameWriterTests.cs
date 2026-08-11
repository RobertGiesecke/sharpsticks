using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// The write-side frame primitives: frames hand-written through
/// <see cref="DescriptorFrameWriter"/> / <see cref="StateFrameWriter"/> decode
/// through the readers, re-writing a decoded descriptor reproduces the
/// original bytes (write(read(x)) == x), and undersized buffers fail cleanly.
/// </summary>
public sealed class OverlayFrameWriterTests : IDisposable
{
	private const double Quantum = 1.0 / 32767.0;

	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void HandWrittenFrames_DecodeThroughTheReaders()
	{
		// A consumer with no JoystickDevice at all writes both frames.
		Span<byte> descriptorFrame = stackalloc byte[64];
		var descriptorWriter = new DescriptorFrameWriter(descriptorFrame, version: 3, deviceCount: 1);
		Assert.True(descriptorWriter.TryWriteDevice(
			isOutput: true, "pad"u8, [Axis.X, Axis.Rz], buttonCount: 10));
		var descriptor = OverlayFrames.ReadDescriptor(descriptorFrame[..descriptorWriter.BytesWritten]);

		var device = Assert.Single(descriptor.Devices);
		Assert.True(device.IsOutput);
		Assert.Equal("pad", device.Name);
		Assert.Equal([Axis.X, Axis.Rz], device.Axes);
		Assert.Equal(10, device.ButtonCount);

		Span<byte> stateFrame = stackalloc byte[64];
		var stateWriter = new StateFrameWriter(stateFrame, version: 3);
		Assert.True(stateWriter.TryBeginDevice(axisCount: 2, buttonCount: 10, out var deviceWriter));
		deviceWriter.WriteAxisValue(0, 0.25);
		deviceWriter.WriteRawAxisValue(1, -32767);
		deviceWriter.SetButton(1);
		deviceWriter.SetButton(10);
		var state = OverlayFrames.ReadState(stateFrame[..stateWriter.BytesWritten], descriptor);

		var decoded = Assert.Single(state.Devices);
		Assert.Equal(0.25, decoded.Axes[0], Quantum);
		Assert.Equal(-1.0, decoded.Axes[1], Quantum);
		Assert.Equal(
			[true, false, false, false, false, false, false, false, false, true],
			decoded.Buttons);
	}

	[Fact]
	public void RewritingADecodedDescriptor_ReproducesTheOriginalBytes()
	{
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X).AddAxis(Axis.Ry).AddButtons(3).Build();
		var mirror = _Fakes.AddInputDevice("vJoy Device")
			.AddAxis(Axis.Z).AddButtons(9).Build();
		var protocol = OverlayProtocol.Create<FakeJoystickDevice>([stick, mirror], version: 7);
		var original = protocol.Descriptor;

		// write(read(x)) == x: pipe every decoded DeviceInfo straight back out.
		Span<byte> rewritten = stackalloc byte[128];
		var writer = new DescriptorFrameWriter(rewritten, version: 7, deviceCount: 2);
		var reader = OverlayFrames.CreateReader(original).GetDescriptorFrameReader().Value;
		foreach (var info in reader.EnumerateDevices())
		{
			Assert.True(writer.TryWriteDevice(info));
		}

		Assert.True(rewritten[..writer.BytesWritten].SequenceEqual(original));
	}

	[Fact]
	public void Writers_FailCleanly_OnUndersizedBuffers()
	{
		Span<byte> tiny = stackalloc byte[8];
		var descriptorWriter = new DescriptorFrameWriter(tiny, version: 1, deviceCount: 1);
		Assert.False(descriptorWriter.TryWriteDevice(
			isOutput: false, "much too long"u8, [Axis.X], buttonCount: 4));
		Assert.Equal(DescriptorFrameWriter.HeaderSize, descriptorWriter.BytesWritten);

		var stateWriter = new StateFrameWriter(tiny, version: 1);
		Assert.False(stateWriter.TryBeginDevice(axisCount: 8, buttonCount: 32, out _));
		Assert.Equal(StateFrameWriter.HeaderSize, stateWriter.BytesWritten);

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new DescriptorFrameWriter(new byte[2], version: 1, deviceCount: 0);
		});
	}
}
