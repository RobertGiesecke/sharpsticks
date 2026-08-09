using SharpSticks.Overlay.WebSockets;

namespace SharpSticks.Tests;

/// <summary>
/// The overlay CLI's argument parsing: ports, web root, device selectors (by
/// id or case-insensitive name part), and the reject paths. The predicate is
/// always exercised AFTER the method returned — that is how the serve path
/// consumes it.
/// </summary>
public sealed class OverlayServeArgsTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void ParsesPortAndWebRoot()
	{
		var options = ServeOptions<FakeJoystickDevice>.BuildServeOptionsFromArgs(
			["serve", "--port", "9000", "--root", @"c:\overlay"]);

		Assert.NotNull(options);
		Assert.Equal((ushort)9000, options.Value.Port);
		Assert.Equal(@"c:\overlay", options.Value.WebRoot);
		Assert.Null(options.Value.DevicePredicate);
	}

	[Fact]
	public void InvalidPort_AndUnknownOption_AreRejected()
	{
		Assert.Null(ServeOptions<FakeJoystickDevice>.BuildServeOptionsFromArgs(["--port", "nope"]));
		Assert.Null(ServeOptions<FakeJoystickDevice>.BuildServeOptionsFromArgs(["--frobnicate"]));
	}

	[Fact]
	public void BareWords_AreIgnored()
	{
		var options = ServeOptions<FakeJoystickDevice>.BuildServeOptionsFromArgs(["serve", "extra"]);

		Assert.NotNull(options);
		Assert.Null(options.Value.Port);
	}

	[Fact]
	public void DeviceSelectors_MatchById_OrByNamePart_AfterTheCallReturns()
	{
		var stick = _Fakes.AddInputDevice("Left Stick", deviceId: 3).Build();
		var pedals = _Fakes.AddInputDevice("Rudder Pedals", deviceId: 7).Build();
		var throttle = _Fakes.AddInputDevice("Throttle", deviceId: 9).Build();

		var options = ServeOptions<FakeJoystickDevice>.BuildServeOptionsFromArgs(
			["--device", "3", "--device", "rudder"]);

		// The serve path evaluates the predicate long after parsing returned;
		// it must still see the parsed selectors.
		var predicate = options?.DevicePredicate;
		Assert.NotNull(predicate);
		Assert.True(predicate!(stick));    // by id
		Assert.True(predicate(pedals));    // by case-insensitive name part
		Assert.False(predicate(throttle));
	}
}
