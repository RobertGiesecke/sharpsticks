using System.Runtime.InteropServices;

namespace ScaledAxisCSharp;

internal sealed class JoystickDevice
{
	public JoystickDevice(int deviceId, JoystickCaps caps)
	{
		DeviceId = deviceId;
		Caps = caps;
	}

	public int DeviceId { get; }
	public JoystickCaps Caps { get; }

	public string Name => Caps.ProductName;

	public bool TryRead(out JoystickState state, out string? error)
	{
		var info = JoyInfoEx.CreateDefault();
		var result = WinMmNative.joyGetPosEx((uint)DeviceId, ref info);
		if (result == WinMmNative.JoyerrNoerror)
		{
			state = new JoystickState(info);
			error = null;
			return true;
		}

		state = default;
		error = result switch
		{
			WinMmNative.JoyerrUnplugged => $"Joystick {DeviceId} is unplugged.",
			WinMmNative.MmsyserrNodriver => "The Windows joystick driver is not available.",
			WinMmNative.MmsyserrBaddeviceid => $"Joystick {DeviceId} is not a valid WinMM device id.",
			_ => $"joyGetPosEx failed for joystick {DeviceId} with error code {result}.",
		};

		return false;
	}

	public double ReadNormalizedAxis(in JoystickState state, AxisBinding binding)
	{
		var range = GetRange(binding.Axis);
		var rawValue = state.GetAxisValue(binding.Axis);
		return Normalize(rawValue, range.Min, range.Max, binding.Mode, binding.Invert, binding.Deadzone);
	}

	public static IReadOnlyList<JoystickDevice> EnumerateConnected()
	{
		var devices = new List<JoystickDevice>();
		var maxDevices = WinMmNative.joyGetNumDevs();

		for (var deviceId = 0; deviceId < maxDevices; deviceId++)
		{
			var caps = JoystickCaps.CreateDefault();
			var capsResult = WinMmNative.joyGetDevCaps((uint)deviceId, ref caps, (uint)Marshal.SizeOf<JoystickCaps>());
			if (capsResult != WinMmNative.JoyerrNoerror)
			{
				continue;
			}

			var info = JoyInfoEx.CreateDefault();
			var posResult = WinMmNative.joyGetPosEx((uint)deviceId, ref info);
			if (posResult is WinMmNative.JoyerrNoerror or WinMmNative.JoyerrUnplugged)
			{
				devices.Add(new JoystickDevice(deviceId, caps));
			}
		}

		return devices;
	}

	private AxisRange GetRange(PhysicalAxis axis)
	{
		return axis switch
		{
			PhysicalAxis.X => new AxisRange(Caps.XMin, Caps.XMax),
			PhysicalAxis.Y => new AxisRange(Caps.YMin, Caps.YMax),
			PhysicalAxis.Z => new AxisRange(Caps.ZMin, Caps.ZMax),
			PhysicalAxis.R => new AxisRange(Caps.RMin, Caps.RMax),
			PhysicalAxis.U => new AxisRange(Caps.UMin, Caps.UMax),
			PhysicalAxis.V => new AxisRange(Caps.VMin, Caps.VMax),
			_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
		};
	}

	private static double Normalize(uint rawValue, uint min, uint max, AxisMode mode, bool invert, double deadzone)
	{
		if (max <= min)
		{
			return mode == AxisMode.Unsigned ? 0.0 : 0.0;
		}

		var range = max - min;
		var normalized = (rawValue - min) / (double)range;
		normalized = Math.Clamp(normalized, 0.0, 1.0);

		if (mode == AxisMode.Signed)
		{
			normalized = normalized * 2.0 - 1.0;
		}

		if (invert)
		{
			normalized = mode == AxisMode.Signed ? -normalized : 1.0 - normalized;
		}

		deadzone = Math.Clamp(deadzone, 0.0, 0.99);
		if (deadzone > 0.0)
		{
			normalized = mode == AxisMode.Signed
				? ApplySignedDeadzone(normalized, deadzone)
				: ApplyUnsignedDeadzone(normalized, deadzone);
		}

		return normalized;
	}

	private static double ApplySignedDeadzone(double value, double deadzone)
	{
		var magnitude = Math.Abs(value);
		if (magnitude <= deadzone)
		{
			return 0.0;
		}

		var adjusted = (magnitude - deadzone) / (1.0 - deadzone);
		return Math.CopySign(adjusted, value);
	}

	private static double ApplyUnsignedDeadzone(double value, double deadzone)
	{
		if (value <= deadzone)
		{
			return 0.0;
		}

		return (value - deadzone) / (1.0 - deadzone);
	}

	private readonly record struct AxisRange(uint Min, uint Max);
}

internal readonly record struct JoystickState(
	uint X,
	uint Y,
	uint Z,
	uint R,
	uint U,
	uint V,
	uint Buttons,
	uint Pov)
{
	public JoystickState(JoyInfoEx info)
		: this(info.XPos, info.YPos, info.ZPos, info.RPos, info.UPos, info.VPos, info.Buttons, info.Pov)
	{
	}

	public uint GetAxisValue(PhysicalAxis axis)
	{
		return axis switch
		{
			PhysicalAxis.X => X,
			PhysicalAxis.Y => Y,
			PhysicalAxis.Z => Z,
			PhysicalAxis.R => R,
			PhysicalAxis.U => U,
			PhysicalAxis.V => V,
			_ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
		};
	}

	public bool IsButtonPressed(int buttonNumber)
	{
		if (buttonNumber < 1 || buttonNumber > 32)
		{
			return false;
		}

		var mask = 1u << (buttonNumber - 1);
		return (Buttons & mask) != 0;
	}
}

[StructLayout(LayoutKind.Sequential)]
internal struct JoyInfoEx
{
	public uint Size;
	public uint Flags;
	public uint XPos;
	public uint YPos;
	public uint ZPos;
	public uint RPos;
	public uint UPos;
	public uint VPos;
	public uint Buttons;
	public uint ButtonNumber;
	public uint Pov;
	public uint Reserved1;
	public uint Reserved2;

	public static JoyInfoEx CreateDefault()
	{
		return new JoyInfoEx
		{
			Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
			Flags = WinMmNative.JoyReturnall,
		};
	}
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct JoystickCaps
{
	public ushort ManufacturerId;
	public ushort ProductId;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinMmNative.Maxpnamelen)]
	public string ProductName;

	public uint XMin;
	public uint XMax;
	public uint YMin;
	public uint YMax;
	public uint ZMin;
	public uint ZMax;
	public uint NumButtons;
	public uint PeriodMin;
	public uint PeriodMax;
	public uint RMin;
	public uint RMax;
	public uint UMin;
	public uint UMax;
	public uint VMin;
	public uint VMax;
	public uint Caps;
	public uint MaxAxes;
	public uint NumAxes;
	public uint MaxButtons;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinMmNative.Maxpnamelen)]
	public string RegistryKey;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinMmNative.Maxpnamelen)]
	public string OemVxD;

	public static JoystickCaps CreateDefault()
	{
		return new JoystickCaps
		{
			ProductName = string.Empty,
			RegistryKey = string.Empty,
			OemVxD = string.Empty,
		};
	}
}

internal static class WinMmNative
{
	public const int JoyerrNoerror = 0;
	public const int MmsyserrNodriver = 6;
	public const int MmsyserrBaddeviceid = 2;
	public const int JoyerrUnplugged = 167;
	public const uint JoyReturnx = 0x00000001;
	public const uint JoyReturny = 0x00000002;
	public const uint JoyReturnz = 0x00000004;
	public const uint JoyReturnr = 0x00000008;
	public const uint JoyReturnu = 0x00000010;
	public const uint JoyReturnv = 0x00000020;
	public const uint JoyReturnpov = 0x00000040;
	public const uint JoyReturnbuttons = 0x00000080;
	public const uint JoyReturnrawdata = 0x00000100;
	public const uint JoyReturnpovcts = 0x00000200;
	public const uint JoyReturncentered = 0x00000400;
	public const uint JoyUsedeadzone = 0x00000800;

	public const uint JoyReturnall = JoyReturnx | JoyReturny | JoyReturnz | JoyReturnr |
	                                 JoyReturnu | JoyReturnv | JoyReturnpov | JoyReturnbuttons;

	public const int Maxpnamelen = 32;

	[DllImport("winmm.dll")]
	public static extern int joyGetNumDevs();

	[DllImport("winmm.dll", EntryPoint = "joyGetDevCapsW", CharSet = CharSet.Unicode)]
	public static extern int joyGetDevCaps(uint joystickId, ref JoystickCaps caps, uint capsSize);

	[DllImport("winmm.dll")]
	public static extern int joyGetPosEx(uint joystickId, ref JoyInfoEx info);
}