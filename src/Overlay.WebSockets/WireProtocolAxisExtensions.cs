namespace SharpSticks.Overlay.WebSockets;

/// <summary>
/// The <see cref="Axis"/>-typed face of the dependency-free wire primitives:
/// Overlay.WireProtocol speaks raw one-byte axis codes; these extensions
/// translate to and from the enum for consumers that have the device
/// abstractions loaded anyway.
/// </summary>
public static class WireProtocolAxisExtensions
{
	extension(in DeviceInfo deviceInfo)
	{
		public Axis GetAxis(int index) => (Axis)deviceInfo.AxesBytes[index];
	}

	extension(ref DescriptorFrameWriter writer)
	{
		public bool TryWriteDevice(
			bool isOutput,
			ReadOnlySpan<byte> nameUtf8,
			ReadOnlySpan<Axis> axes,
			byte buttonCount)
		{
			if (axes.Length > byte.MaxValue)
			{
				return false;
			}

			Span<byte> axisCodes = stackalloc byte[axes.Length];
			for (var i = 0; i < axes.Length; i++)
			{
				axisCodes[i] = (byte)axes[i];
			}

			return writer.TryWriteDevice(isOutput, nameUtf8, axisCodes, buttonCount);
		}
	}
}
