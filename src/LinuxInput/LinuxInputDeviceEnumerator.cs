using System.Globalization;
using System.IO;

namespace SharpSticks.LinuxInput;

/// Scans <c>/dev/input/event*</c>, opens each device read-only, queries its capability
/// bitmasks via ioctl, and keeps only the ones that look like joysticks or gamepads.
internal static class LinuxInputDeviceEnumerator
{
	private const string InputDir = "/dev/input";

	public static ImmutableArray<LinuxInputDeviceInfo> EnumerateConnectedDeviceInfos()
	{
		if (!Directory.Exists(InputDir))
		{
			return [];
		}

		var builder = ImmutableArray.CreateBuilder<LinuxInputDeviceInfo>();
		var deviceIdCounter = 0;
		foreach (var path in Directory.EnumerateFiles(InputDir, "event*"))
		{
			if (TryProbeDevice(path, deviceIdCounter, out var info))
			{
				builder.Add(info);
				deviceIdCounter++;
			}
		}

		return builder.ToImmutable();
	}

	internal static bool TryProbeDevice(string path, int deviceId, out LinuxInputDeviceInfo info)
	{
		info = default;
		var fd = LinuxInputNative.Open(path,
			LinuxInputEventCodes.OReadOnly | LinuxInputEventCodes.ONonBlock | LinuxInputEventCodes.OCloseOnExec);
		if (fd < 0)
		{
			return false;
		}

		try
		{
			Span<byte> keyBits = stackalloc byte[(LinuxInputEventCodes.BtnDigi / 8) + 1];
			if (!TryGetBits(fd, LinuxInputEventCodes.EvKey, keyBits))
			{
				return false;
			}

			if (!HasJoystickOrGamepadButton(keyBits))
			{
				return false;
			}

			Span<byte> absBits = stackalloc byte[(LinuxInputEventCodes.AbsMax / 8) + 1];
			TryGetBits(fd, LinuxInputEventCodes.EvAbs, absBits);

			var name = ReadStringIoctl(fd, LinuxInputEventCodes.EviocgName(256));
			var uniq = ReadStringIoctl(fd, LinuxInputEventCodes.EviocgUniq(256));

			var inputId = default(LinuxInputId);
			LinuxInputNative.IoctlInputId(fd, LinuxInputEventCodes.EviocgId, ref inputId);

			var axes = ImmutableArray.CreateBuilder<Axis>(8);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsX, Axis.X, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsY, Axis.Y, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsZ, Axis.Z, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsRx, Axis.Rx, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsRy, Axis.Ry, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsRz, Axis.Rz, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsThrottle, Axis.Slider1, axes);
			AddAxisIfPresent(absBits, LinuxInputEventCodes.AbsRudder, Axis.Slider2, axes);

			var buttonCodes = ImmutableArray.CreateBuilder<ushort>(32);
			for (ushort code = LinuxInputEventCodes.BtnJoystick; code < LinuxInputEventCodes.BtnDigi; code++)
			{
				if (TestBit(keyBits, code))
				{
					buttonCodes.Add(code);
				}
			}

			info = new(
				deviceId,
				path,
				string.IsNullOrEmpty(name) ? Path.GetFileName(path) : name,
				string.IsNullOrEmpty(uniq) ? Path.GetFileName(path) : uniq,
				BuildStableGuid(inputId, uniq, path),
				axes.ToImmutable(),
				buttonCodes.ToImmutable());
			return true;
		}
		finally
		{
			LinuxInputNative.Close(fd);
		}
	}

	private static bool TryGetBits(int fd, uint evType, Span<byte> bits)
	{
		bits.Clear();
		var result = LinuxInputNative.IoctlBuffer(fd,
			LinuxInputEventCodes.EviocgBit(evType, (uint)bits.Length),
			ref MemoryMarshal.GetReference(bits));
		return result >= 0;
	}

	private static bool HasJoystickOrGamepadButton(ReadOnlySpan<byte> keyBits)
	{
		for (ushort code = LinuxInputEventCodes.BtnJoystick; code < LinuxInputEventCodes.BtnDigi; code++)
		{
			if (TestBit(keyBits, code))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TestBit(ReadOnlySpan<byte> bits, int bit)
	{
		var byteIndex = bit >> 3;
		if (byteIndex >= bits.Length)
		{
			return false;
		}

		return (bits[byteIndex] & (1 << (bit & 7))) != 0;
	}

	private static void AddAxisIfPresent(
		ReadOnlySpan<byte> absBits,
		ushort linuxCode,
		Axis axis,
		ImmutableArray<Axis>.Builder target)
	{
		if (TestBit(absBits, linuxCode))
		{
			target.Add(axis);
		}
	}

	private static string ReadStringIoctl(int fd, uint request)
	{
		Span<byte> buffer = stackalloc byte[256];
		var result = LinuxInputNative.IoctlBuffer(fd, request, ref MemoryMarshal.GetReference(buffer));
		if (result <= 0)
		{
			return string.Empty;
		}

		var length = Math.Min(result, buffer.Length);
		var nul = buffer[..length].IndexOf((byte)0);
		if (nul >= 0)
		{
			length = nul;
		}

		return System.Text.Encoding.UTF8.GetString(buffer[..length]);
	}

	private static Guid BuildStableGuid(LinuxInputId id, string uniq, string path)
	{
		// Deterministic UUIDv5-style hash from the device identity. Matches DirectInput's
		// promise that the GUID survives across sessions for the same device instance.
		var input = string.Create(CultureInfo.InvariantCulture,
			$"{id.BusType:x4}:{id.Vendor:x4}:{id.Product:x4}:{id.Version:x4}:{uniq}:{path}");
		var bytes = System.Text.Encoding.UTF8.GetBytes(input);
		Span<byte> hash = stackalloc byte[16];
		System.Security.Cryptography.MD5.HashData(bytes, hash);
		return new Guid(hash);
	}
}
