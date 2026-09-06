using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SharpSticks.VJoy;

namespace SharpSticks.Tests;

public sealed class VJoyHidReportDescriptorTests
{
	/// Descriptor vJoyConf 2.1.x writes for a slot with all 8 axes, 4 continuous POVs and
	/// 128 buttons (captured from a real registry, "HidReportDesctiptor" value).
	private static readonly byte[] ClassicDescriptor = FromHex(
		"05 01 15 00 09 04 A1 01 05 01 85 01 09 01 15 00 26 FF 7F 75 20 95 01 A1 00 " +
		"09 30 81 02 09 31 81 02 09 32 81 02 09 33 81 02 09 34 81 02 09 35 81 02 09 36 81 02 09 37 81 02 C0 " +
		"15 00 27 3C 8C 00 00 35 00 47 3C 8C 00 00 65 14 75 20 95 01 " +
		"09 39 81 02 09 39 81 02 09 39 81 02 09 39 81 02 " +
		"05 09 15 00 25 01 55 00 65 00 19 01 29 80 75 01 95 80 81 02 C0");

	/// Same slot as written by a 2.2.x fork ("HidReportDescriptor" value): adds the wheel,
	/// accelerator, brake, clutch, steering, aileron, rudder and throttle axes.
	private static readonly byte[] ExtendedAxesDescriptor = FromHex(
		"05 01 15 00 09 04 A1 01 05 01 85 01 09 01 15 00 26 FF 7F 75 20 95 01 A1 00 " +
		"09 30 81 02 09 31 81 02 09 32 81 02 09 33 81 02 09 34 81 02 09 35 81 02 09 36 81 02 09 37 81 02 " +
		"09 38 81 02 09 C4 81 02 09 C5 81 02 09 C6 81 02 09 C8 81 02 09 B0 81 02 09 BA 81 02 09 BB 81 02 C0 " +
		"15 00 27 3C 8C 00 00 35 00 47 3C 8C 00 00 65 14 75 20 95 01 " +
		"09 39 81 02 09 39 81 02 09 39 81 02 09 39 81 02 " +
		"05 09 15 00 25 01 55 00 65 00 19 01 29 80 75 01 95 80 81 02 C0");

	/// Hand-built: X, Y, Rz, Slider1 (the latter as a 4-byte extended usage), a Z declared
	/// as a Constant input, constant padding, 12 buttons, and an Output report on the button
	/// page that must not count.
	private static readonly byte[] SubsetDescriptor = FromHex(
		"05 01 09 04 A1 01 85 02 15 00 26 FF 7F 75 20 95 01 " +
		"09 30 81 02 09 31 81 02 09 35 81 02 0B 36 00 01 00 81 02 " +
		"09 32 81 03 75 20 95 02 81 01 " +
		"05 09 19 01 29 0C 15 00 25 01 75 01 95 0C 81 02 " +
		"05 09 19 01 29 08 75 01 95 08 91 02 " +
		"75 04 95 01 81 01 C0");

	private static readonly ImmutableArray<Axis> AllEightAxes =
		[Axis.X, Axis.Y, Axis.Z, Axis.Rx, Axis.Ry, Axis.Rz, Axis.Slider1, Axis.Slider2];

	[Fact]
	public void ClassicDescriptor_YieldsAllAxesAndButtons()
	{
		Assert.Equal(115, ClassicDescriptor.Length);

		Assert.True(VJoyHidReportDescriptor.TryParse(ClassicDescriptor, out var axes, out var buttonCount));
		Assert.Equal<Axis>(AllEightAxes, axes);
		Assert.Equal(128u, buttonCount);
	}

	[Fact]
	public void ExtendedAxesDescriptor_ReportsOnlyTheRoutableAxes()
	{
		Assert.Equal(147, ExtendedAxesDescriptor.Length);

		Assert.True(VJoyHidReportDescriptor.TryParse(ExtendedAxesDescriptor, out var axes, out var buttonCount));
		Assert.Equal<Axis>(AllEightAxes, axes);
		Assert.Equal(128u, buttonCount);
	}

	[Fact]
	public void SubsetDescriptor_SkipsConstantsPaddingAndOutputReports()
	{
		Assert.True(VJoyHidReportDescriptor.TryParse(SubsetDescriptor, out var axes, out var buttonCount));
		Assert.Equal<Axis>([Axis.X, Axis.Y, Axis.Rz, Axis.Slider1], axes);
		Assert.Equal(12u, buttonCount);
	}

	[Fact]
	public void ButtonRange_IsCappedByReportCount()
	{
		// Usage range 1..32 but only 16 report fields.
		var descriptor = FromHex("05 09 19 01 29 20 15 00 25 01 75 01 95 10 81 02");

		Assert.True(VJoyHidReportDescriptor.TryParse(descriptor, out var axes, out var buttonCount));
		Assert.Empty(axes);
		Assert.Equal(16u, buttonCount);
	}

	[Fact]
	public void TruncatedDescriptor_Fails()
	{
		// The 0x26 item announces two data bytes; only one follows.
		Assert.False(VJoyHidReportDescriptor.TryParse(FromHex("05 01 09 04 A1 01 26 FF"), out _, out _));
	}

	[Fact]
	public void EmptyDescriptor_ParsesToNothing()
	{
		Assert.True(VJoyHidReportDescriptor.TryParse([], out var axes, out var buttonCount));
		Assert.Empty(axes);
		Assert.Equal(0u, buttonCount);
	}

	/// The design-time enumeration is what the source generator runs inside Rider / the
	/// compiler server. It must not drag vJoyInterface.dll into that process, because the
	/// DLL keeps a device handle open and thereby locks the slot against the real feeder.
	[Fact]
	public void EnumerateAvailableOutputs_DoesNotLoadVJoyInterface()
	{
		_ = VJoyDeviceFactory.Instance.EnumerateAvailableOutputs();

		using var process = Process.GetCurrentProcess();
		Assert.DoesNotContain(
			process.Modules.Cast<ProcessModule>(),
			static module => module.ModuleName.Contains("vJoyInterface", StringComparison.OrdinalIgnoreCase));
	}

	public static bool HasConfiguredSlotOne => OperatingSystem.IsWindows() && SlotOneIsConfigured();

	[Fact(Skip = "vJoy slot 1 is not configured on this machine", SkipUnless = nameof(HasConfiguredSlotOne))]
	public void EnumerateAvailableOutputs_ReportsTheConfiguredSlot()
	{
		var outputs = VJoyDeviceFactory.Instance.EnumerateAvailableOutputs();

		var slotOne = Assert.Single(outputs, static output => output.DeviceId == 1);
		Assert.NotEmpty(slotOne.Axes);
		Assert.True(slotOne.ButtonCount > 0);
		Assert.Equal(VirtualOutputProducts.VJoyProductGuid, slotOne.InputProductGuid);
	}

	[SupportedOSPlatform("windows")]
	private static bool SlotOneIsConfigured()
	{
		using var key = Registry.LocalMachine.OpenSubKey(
			@"SYSTEM\CurrentControlSet\Services\vjoy\Parameters\Device01", writable: false);
		return key is not null;
	}

	private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", ""));
}
