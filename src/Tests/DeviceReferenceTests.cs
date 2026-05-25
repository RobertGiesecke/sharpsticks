namespace SharpSticks.Tests;

/// <summary>
/// Covers <see cref="AppConfig.Devices"/>: capture (walk bindings + modifiers
/// to populate device entries), JSON round-trip, resolve (GUID → Name with
/// sorted-id fallback), and end-to-end through Runtime when the actual
/// device ids differ from the config's authored ids.
/// </summary>
public sealed class DeviceReferenceTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	// ── CaptureDevices walks bindings + modifier nesting ────────────────

	[Fact]
	public void CaptureDevices_PopulatesEntriesForEveryReferencedId_IncludingNestedModifierIds()
	{
		var stickA = _Fakes.AddInputDevice("Stick A").AddAxis(Axis.X).AddButtons(2).Build();
		var stickB = _Fakes.AddInputDevice("Stick B").AddAxis(Axis.Slider1).AddButtons(2).Build();

		var config = new AppConfig
		{
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(stickA.DeviceId, 1),
					TargetButton = 1,
				},
			],
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = stickA.DeviceId, Axis = "x" },
					TargetAxis = "x",
					Modifier = new WhenButtonPressedAxisModifier
					{
						// referenced device id 2 — nested inside the modifier
						Buttons = [new(stickB.DeviceId, 1)],
						WhenPressed = new AxisCurve { Max = 0.5 },
						WhenNotPressed = new BlendedAxisCurve
						{
							NormalCurve = new AxisCurve { Max = 1.0 },
							PrecisionCurve = new AxisCurve { Max = 0.2 },
							// also device id 2 via the modifier axis
							ModifierAxis = new(stickB.DeviceId, Axis.Slider1, AxisMode.Unsigned),
						},
					},
				},
			},
		};

		config.CaptureDevices(_Fakes.InputDevices);

		Assert.Equal(2, config.Devices.Count);
		Assert.Contains(config.Devices, d => d.DeviceId == stickA.DeviceId && d.Name == "Stick A");
		Assert.Contains(config.Devices, d => d.DeviceId == stickB.DeviceId && d.Name == "Stick B");
		Assert.All(config.Devices, d => Assert.NotNull(d.InstanceGuid));
	}

	[Fact]
	public void CaptureDevices_EmptyWhenNothingIsReferenced()
	{
		_Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();

		var config = new AppConfig();
		config.CaptureDevices(_Fakes.InputDevices);

		Assert.Empty(config.Devices);
	}

	// ── JSON round-trip of Devices ──────────────────────────────────────

	[Fact]
	public void Json_DevicesList_RoundTrips()
	{
		var original = new AppConfig
		{
			Devices =
			{
				new()
				{
					DeviceId = 3, Name = "VPC Stick", InstanceGuid = Guid.Parse("00000000-0000-0000-0000-000000000001")
				},
				new() { DeviceId = 7, Name = "Other Stick", InstanceGuid = null },
			},
		};

		var json = JsonSerializer.Serialize(original, AppJsonContext.Default.AppConfig);
		var copy = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);

		Assert.NotNull(copy);
		Assert.Equal(2, copy!.Devices.Count);
		Assert.Equal(3, copy.Devices[0].DeviceId);
		Assert.Equal("VPC Stick", copy.Devices[0].Name);
		Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), copy.Devices[0].InstanceGuid);
		Assert.Null(copy.Devices[1].InstanceGuid);
	}

	// ── ResolveDeviceMap ────────────────────────────────────────────────

	[Fact]
	public void Resolve_EmptyDevicesList_ReturnsEmptyMap()
	{
		_Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();

		using var map = new PooledDictionary<int, int>();
		new AppConfig().ResolveDeviceMap(_Fakes.InputDevices, map);

		Assert.Empty(map);
	}

	[Fact]
	public void Resolve_MatchesByInstanceGuid_AcrossDifferentDeviceIds()
	{
		// Stick at config-time was DeviceId=3; today the platform gave it id 7.
		var guid = Guid.NewGuid();
		_Fakes.AddInputDevice("Filler").AddAxis(Axis.X).Build(); // id 1
		_Fakes.AddInputDevice("Filler 2").AddAxis(Axis.X).Build(); // id 2
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X)
			.WithInstanceGuid(guid).Build(); // id 3 (we want this matched)

		var config = new AppConfig
		{
			Devices = { new() { DeviceId = 99, Name = "wrong name on purpose", InstanceGuid = guid } },
		};

		using var map = new PooledDictionary<int, int>();
		config.ResolveDeviceMap(_Fakes.InputDevices, map);

		Assert.Single(map);
		Assert.Equal(stick.DeviceId, map[99]); // GUID wins even though Name doesn't match.
	}

	[Fact]
	public void Resolve_FallsBackToName_WhenGuidIsUnknownOrMissing()
	{
		_Fakes.AddInputDevice("Filler").AddAxis(Axis.X).Build();
		var stick = _Fakes.AddInputDevice("Target").AddAxis(Axis.X).Build();

		var config = new AppConfig
		{
			Devices = { new() { DeviceId = 42, Name = "Target" /* no GUID */ } },
		};

		using var map = new PooledDictionary<int, int>();
		config.ResolveDeviceMap(_Fakes.InputDevices, map);

		Assert.Equal(stick.DeviceId, map[42]);
	}

	[Fact]
	public void Resolve_SameNameMultipleDevices_PairsBySortedDeviceId()
	{
		// Two "Stick" devices in the config, two "Stick" connected. No GUIDs.
		// Config's "Stick" with lowest id pairs with connected lowest id, etc.
		var first = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build(); // id 1
		_Fakes.AddInputDevice("Filler").AddAxis(Axis.X).Build(); // id 2
		var second = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build(); // id 3

		var config = new AppConfig
		{
			Devices =
			{
				new() { DeviceId = 100, Name = "Stick" }, // lowest config id
				new() { DeviceId = 200, Name = "Stick" },
			},
		};

		using var map = new PooledDictionary<int, int>();
		config.ResolveDeviceMap(_Fakes.InputDevices, map);

		Assert.Equal(first.DeviceId, map[100]);
		Assert.Equal(second.DeviceId, map[200]);
	}

	[Fact]
	public void Resolve_GuidMatchesTakePrecedence_RemainingPairBySortedId()
	{
		// One "Stick" matched by GUID, the rest matched by sorted-id fallback.
		var guid = Guid.NewGuid();
		_Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build(); // id 1, no special guid
		var byGuid = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X)
			.WithInstanceGuid(guid).Build(); // id 2 (this is the guid-matched one)
		var firstByName = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build(); // id 3

		var config = new AppConfig
		{
			Devices =
			{
				new() { DeviceId = 100, Name = "Stick" /* no guid */ },
				new() { DeviceId = 200, Name = "Stick" /* no guid */ },
				new() { DeviceId = 300, Name = "Stick", InstanceGuid = guid },
			},
		};

		using var map = new PooledDictionary<int, int>();
		config.ResolveDeviceMap(_Fakes.InputDevices, map);

		// GUID-matched: 300 → byGuid (id 2)
		Assert.Equal(byGuid.DeviceId, map[300]);
		// Sorted-id pairs the remaining configs (100, 200) with currents (1, 3).
		Assert.Equal(1, map[100]);
		Assert.Equal(firstByName.DeviceId, map[200]);
	}

	[Fact]
	public void Resolve_Throws_WhenNoMatchingDeviceIsConnected()
	{
		_Fakes.AddInputDevice("PresentStick").AddAxis(Axis.X).Build();

		var config = new AppConfig
		{
			Devices = { new() { DeviceId = 1, Name = "MissingStick" } },
		};

		using var map = new PooledDictionary<int, int>();
		Assert.Throws<InvalidOperationException>(() => config.ResolveDeviceMap(_Fakes.InputDevices, map));
	}

	// ── End-to-end: load with different ids, runtime works ──────────────

	[Fact]
	public void EndToEnd_ConfigAuthoredAgainstOneIdSet_LoadsAgainstDifferentIds()
	{
		// Imagine: yesterday the user authored the config with the stick at
		// id 5. Today the stick is at id 1. The config's Devices entry lets
		// us recognize the same device and rewrite the bindings.
		const int configStickId = 5;
		var connectedGuid = Guid.NewGuid();
		var stick = _Fakes.AddInputDevice("Stick")
			.AddAxis(Axis.X)
			.AddButtons(2)
			.WithInstanceGuid(connectedGuid)
			.Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).AddButtons(4).Build();

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			Devices =
			{
				new() { DeviceId = configStickId, Name = "Stick", InstanceGuid = connectedGuid },
			},
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(configStickId, 1),
					TargetButton = 3,
				},
			],
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = configStickId, Axis = "x" },
					TargetAxis = "x",
					Modifier = new WhenButtonPressedAxisModifier
					{
						Buttons = [new(configStickId, 2)],
						WhenPressed = new AxisCurve { Max = 0.5 },
						WhenNotPressed = new AxisCurve { Max = 1.0 },
					},
				},
			},
		};

		using var deviceMap = new PooledDictionary<int, int>();
		config.ResolveDeviceMap(_Fakes.InputDevices, deviceMap);
		Assert.Equal(stick.DeviceId, deviceMap[configStickId]);

		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = config.BuildRoutes(deviceMap),
		});

		// Button route works against the real stick id.
		stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(output.GetButtonState(3));

		// Axis route, no modifier button pressed → identity.
		stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.6, output.GetAxisValue(Axis.X), Precision);

		// Modifier's nested ButtonBinding(configStickId, 2) was also translated
		// to the real stick id, so pressing button 2 takes effect.
		stick.PressButton(2);
		runtime.ProcessFrame();
		Assert.Equal(0.3, output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void EndToEnd_CaptureThenRoundTripThroughJson_StillResolvesAfterIdShuffle()
	{
		// 1. Author against the current devices, capture their identity.
		var saveTimeGuid = Guid.NewGuid();
		_Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2)
			.WithInstanceGuid(saveTimeGuid).Build();
		var outputAtSave = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		var config = new AppConfig
		{
			VJoyDeviceId = outputAtSave.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = 1, Axis = "x" },
					TargetAxis = "x",
					Modifier = new AxisCurve { Max = 0.5 },
				},
			},
		};
		config.CaptureDevices(_Fakes.InputDevices);
		var json = JsonSerializer.Serialize(config, typeof(AppConfig), AppJsonContext.Polymorphic);

		// 2. Simulate the next session: a different FakeDeviceManager with
		// a filler device claiming id 1 first, then the real stick at id 2.
		using var nextSession = new FakeDeviceManager();
		nextSession.AddInputDevice("Some other device").AddAxis(Axis.X).Build(); // id 1
		var reconnected = nextSession.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(2)
			.WithInstanceGuid(saveTimeGuid).Build(); // id 2
		var output = nextSession.AddOutputDevice().AddAxis(Axis.X).Build();

		var loaded = (AppConfig?)JsonSerializer.Deserialize(json, typeof(AppConfig), AppJsonContext.Polymorphic);
		Assert.NotNull(loaded);
		loaded!.VJoyDeviceId = output.DeviceId;

		using var map = new PooledDictionary<int, int>();
		loaded.ResolveDeviceMap(nextSession.InputDevices, map);
		Assert.Equal(reconnected.DeviceId, map[1]); // config-side id 1 → today's id 2

		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = nextSession.InputDevices,
			OutputDeviceFactory = nextSession.OutputDeviceFactory,
			Routes = loaded.BuildRoutes(map),
		});

		reconnected.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.4, output.GetAxisValue(Axis.X), Precision); // 0.5x curve applied
	}
}