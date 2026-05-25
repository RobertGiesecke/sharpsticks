namespace SharpSticks.Tests;

/// <summary>
/// Covers the <see cref="AppConfig"/> surface: JSON round-trips for the
/// declarative parts (mappings without modifiers, which are C#-only),
/// parser fan-out (Axis / AxisMode / AxisBinding), validation, and
/// end-to-end materialization through <see cref="AppConfig.BuildRoutes"/>
/// into a <see cref="Runtime"/> driven by fakes — including all four
/// modifier types wired through the config object.
/// </summary>
public sealed class AppConfigTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	// ── JSON round-trip ─────────────────────────────────────────────────

	[Fact]
	public void Json_EmptyConfig_RoundTrips()
	{
		var original = new AppConfig();
		var json = Serialize(original);
		var copy = Deserialize(json);

		Assert.NotNull(copy);
		Assert.Null(copy!.Name);
		Assert.Equal(1u, copy.VJoyDeviceId);
		Assert.Empty(copy.ButtonMappings);
		Assert.Empty(copy.AxisMappings);
	}

	[Fact]
	public void Json_ConfigWithButtonAndAxisMappings_RoundTrips()
	{
		var original = new AppConfig
		{
			Name = "fancy profile",
			VJoyDeviceId = 2,
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(3, 1),
					TargetButton = 12,
				},
				new()
				{
					SourceBinding = new(5, 7),
					VJoyDeviceId = 3,
					TargetButton = 4,
				},
			],
			AxisMappings =
			{
				new()
				{
					Source = new()
					{
						DeviceId = 3,
						Axis = "y",
						Mode = "unsigned",
						Invert = true,
						Deadzone = 0.05,
					},
					TargetAxis = "rz",
					Scale = 0.7,
					Offset = -0.1,
					OutputDeviceId = 4,
				},
			},
		};
		var copy = Deserialize(Serialize(original));

		Assert.NotNull(copy);
		Assert.Equal("fancy profile", copy!.Name);
		Assert.Equal(2u, copy.VJoyDeviceId);

		Assert.Equal(2, copy.ButtonMappings.Count);
		Assert.Equal(3, copy.ButtonMappings[0].SourceBinding.DeviceId);
		Assert.Equal(12, copy.ButtonMappings[0].TargetButton);
		Assert.Null(copy.ButtonMappings[0].VJoyDeviceId);
		Assert.Equal(3u, copy.ButtonMappings[1].VJoyDeviceId);

		Assert.Single(copy.AxisMappings);
		var axis = copy.AxisMappings[0];
		Assert.Equal(3, axis.Source.DeviceId);
		Assert.Equal("y", axis.Source.Axis);
		Assert.Equal("unsigned", axis.Source.Mode);
		Assert.True(axis.Source.Invert);
		Assert.Equal(0.05, axis.Source.Deadzone, Precision);
		Assert.Equal("rz", axis.TargetAxis);
		Assert.Equal(0.7, axis.Scale, Precision);
		Assert.Equal(-0.1, axis.Offset, Precision);
		Assert.Equal(4u, axis.OutputDeviceId);
	}

	// ── Parsers ─────────────────────────────────────────────────────────

	[Theory]
	[InlineData("x", Axis.X)]
	[InlineData("X", Axis.X)]
	[InlineData("y", Axis.Y)]
	[InlineData("z", Axis.Z)]
	[InlineData("r", Axis.Rx)]
	[InlineData("rx", Axis.Rx)]
	[InlineData("ry", Axis.Ry)]
	[InlineData("rz", Axis.Rz)]
	[InlineData("u", Axis.Slider1)]
	[InlineData("slider1", Axis.Slider1)]
	[InlineData("v", Axis.Slider2)]
	[InlineData("slider2", Axis.Slider2)]
	[InlineData("  Slider1  ", Axis.Slider1)]
	public void AxisParser_AcceptsAliasesAndIsCaseInsensitiveWithTrim(string input, Axis expected)
	{
		Assert.Equal(expected, Axis.Parse(input));
	}

	[Fact]
	public void AxisParser_RejectsUnknownInput()
	{
		Assert.Throws<InvalidOperationException>(() => Axis.Parse("nonsense"));
	}

	[Theory]
	[InlineData("signed", AxisMode.Signed)]
	[InlineData("SIGNED", AxisMode.Signed)]
	[InlineData("unsigned", AxisMode.Unsigned)]
	[InlineData("Unsigned", AxisMode.Unsigned)]
	public void AxisModeParser_AcceptsBothCasings(string input, AxisMode expected)
	{
		Assert.Equal(expected, AxisMode.Parse(input));
	}

	[Fact]
	public void AxisModeParser_RejectsUnknownInput()
	{
		Assert.Throws<InvalidOperationException>(() => AxisMode.Parse("maybe"));
	}

	[Fact]
	public void AxisBindingParser_ProducesMatchingBinding()
	{
		var input = new AxisInput
		{
			DeviceId = 4,
			Axis = "slider1",
			Mode = "unsigned",
			Invert = true,
			Deadzone = 0.1,
		};

		var binding = AxisBinding.Parse(input);

		Assert.Equal(4, binding.DeviceId);
		Assert.Equal(Axis.Slider1, binding.Axis);
		Assert.Equal(AxisMode.Unsigned, binding.Mode);
		Assert.True(binding.Invert);
		Assert.Equal(0.1, binding.Deadzone, Precision);
	}

	[Fact]
	public void AxisBindingParser_NullInput_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => AxisBinding.Parse(null!));
	}

	// ── BuildRoutes (config → routes) ────────────────────────────────────

	[Fact]
	public void BuildRoutes_EmptyConfig_ProducesEmpty()
	{
		var routes = new AppConfig().BuildRoutes();
		Assert.Empty(routes);
	}

	[Fact]
	public void BuildRoutes_UsesConfigVJoyDeviceId_AsDefaultForBothMappingKinds()
	{
		var config = new AppConfig
		{
			VJoyDeviceId = 7,
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(1, 1),
					TargetButton = 1,
				},
			],
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = 1, Axis = "x" },
					TargetAxis = "x",
				},
			},
		};

		var routes = config.BuildRoutes();
		var button = routes.OfType<ButtonRoute>().Single();
		var axis = routes.OfType<AxisRoute>().Single();

		Assert.Equal(7u, button.OutputBinding.OutputDeviceId);
		Assert.Equal(7u, axis.OutputBinding.OutputDeviceId);
	}

	[Fact]
	public void BuildRoutes_PerMappingDeviceId_OverridesConfigDefault()
	{
		var config = new AppConfig
		{
			VJoyDeviceId = 1,
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(1, 1),
					VJoyDeviceId = 9,
					TargetButton = 1,
				},
			],
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = 1, Axis = "x" },
					TargetAxis = "y",
					OutputDeviceId = 11,
				},
			},
		};

		var routes = config.BuildRoutes();
		Assert.Equal(9u, routes.OfType<ButtonRoute>().Single().OutputBinding.OutputDeviceId);
		Assert.Equal(11u, routes.OfType<AxisRoute>().Single().OutputBinding.OutputDeviceId);
	}

	[Fact]
	public void BuildRoutes_RejectsZeroOrNegativeButtonNumbers()
	{
		var withBadSource = new AppConfig
		{
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(1, 0),
					TargetButton = 1,
				},
			],
		};
		Assert.Throws<InvalidOperationException>(() => withBadSource.BuildRoutes());

		var withBadTarget = new AppConfig
		{
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(1, 1),
					TargetButton = 0,
				},
			],
		};
		Assert.Throws<InvalidOperationException>(() => withBadTarget.BuildRoutes());
	}

	// ── End-to-end materialization through Runtime ──────────────────────

	[Fact]
	public void Materialize_ButtonRoute_DrivesOutput()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).AddButtons(8).Build();

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			ButtonMappings =
			[
				new()
				{
					SourceBinding = new(stick.DeviceId, 1),
					TargetButton = 5,
				},
			],
		};

		using var runtime = BuildRuntime(config);
		stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.True(output.GetButtonState(5));

		stick.ReleaseButton(1);
		runtime.ProcessFrame();
		Assert.False(output.GetButtonState(5));
	}

	[Fact]
	public void Materialize_AxisRoute_WithScaleAndOffset_DrivesOutput()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.Slider1).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.Rz).Build();

		// Source is unsigned slider [0..1]. Scale 2, Offset -1 → maps to [-1..1].
		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new()
					{
						DeviceId = stick.DeviceId,
						Axis = "slider1",
						Mode = "unsigned",
					},
					TargetAxis = "rz",
					Scale = 2.0,
					Offset = -1.0,
				},
			},
		};

		using var runtime = BuildRuntime(config);

		stick.SetAxisValue(Axis.Slider1, 0.0);
		runtime.ProcessFrame();
		Assert.Equal(-1.0, output.GetAxisValue(Axis.Rz), Precision);

		stick.SetAxisValue(Axis.Slider1, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(0.0, output.GetAxisValue(Axis.Rz), Precision);

		stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(1.0, output.GetAxisValue(Axis.Rz), Precision);
	}

	[Fact]
	public void Materialize_AxisRoute_HonorsInvertAndDeadzoneFromSource()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new()
					{
						DeviceId = stick.DeviceId,
						Axis = "x",
						Mode = "signed",
						Invert = true,
					},
					TargetAxis = "x",
				},
			},
		};
		using var runtime = BuildRuntime(config);

		stick.SetAxisValue(Axis.X, 0.5);
		runtime.ProcessFrame();
		Assert.Equal(-0.5, output.GetAxisValue(Axis.X), Precision);

		stick.SetAxisValue(Axis.X, -0.3);
		runtime.ProcessFrame();
		Assert.Equal(0.3, output.GetAxisValue(Axis.X), Precision);
	}

	// ── Modifier wiring smoke tests via AppConfig ───────────────────────

	[Fact]
	public void Materialize_AxisRoute_WithAxisCurveModifier()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = stick.DeviceId, Axis = "x" },
					TargetAxis = "x",
					Modifier = new AxisCurve { Max = 0.5 },
				},
			},
		};
		using var runtime = BuildRuntime(config);

		stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.5, output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Materialize_AxisRoute_WithBlendedAxisCurveModifier()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddAxis(Axis.Slider1).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		var modifier = new BlendedAxisCurve
		{
			NormalCurve = new AxisCurve { Max = 1.0 },
			PrecisionCurve = new AxisCurve { Max = 0.5 },
			ModifierAxis = stick.BindAxis(Axis.Slider1) with { Mode = AxisMode.Unsigned },
		};

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = stick.DeviceId, Axis = "x" },
					TargetAxis = "x",
					Modifier = modifier,
				},
			},
		};
		using var runtime = BuildRuntime(config);

		// Modifier at rest → normal curve → output equals input.
		stick.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();
		Assert.Equal(0.8, output.GetAxisValue(Axis.X), Precision);

		// Modifier fully engaged → precision curve → output halved.
		stick.SetAxisValue(Axis.Slider1, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(0.4, output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Materialize_AxisRoute_WithWhenButtonPressedModifier()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		var output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [stick.BindButton(1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new AxisCurve { Max = 1.0 },
		};

		var config = new AppConfig
		{
			VJoyDeviceId = output.DeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = stick.DeviceId, Axis = "x" },
					TargetAxis = "x",
					Modifier = modifier,
				},
			},
		};
		using var runtime = BuildRuntime(config);

		stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(0.6, output.GetAxisValue(Axis.X), Precision);

		stick.PressButton(1);
		runtime.ProcessFrame();
		Assert.Equal(0.3, output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Materialize_AxisRoutePair_FromRouteAbsoluteRelative()
	{
		// The absolute-relative feature isn't expressed inside AxisMapping;
		// it produces two AxisRoute objects directly. We still verify the
		// pair wires through Runtime when used alongside an AppConfig that
		// supplies the rest of the routing.
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		var output = _Fakes.AddOutputDevice()
			.AddAxis(Axis.Slider1)
			.AddAxis(Axis.Slider2)
			.Build();

		var routes = stick.BindAxis(Axis.X).RouteAbsoluteRelative(new()
		{
			IncreaseAxis = output.BindAxis(Axis.Slider1),
			DecreaseAxis = output.BindAxis(Axis.Slider2),
			InitialValue = 0.0,
			Minimum = 0.0,
			Maximum = 1.0,
			SourceInputMinimum = 0.0,
			SourceInputMaximum = 1.0,
			IncreaseRestPosition = 0.5,
			DecreaseRestPosition = 0.5,
			OutputRiseRate = 1.0,
			Gain = 1.0,
		});

		using var runtime = FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = [..routes],
		});

		stick.SetAxisValue(Axis.X, 1.0);
		runtime.ProcessFrame();
		Assert.Equal(1.0, output.GetAxisValue(Axis.Slider1), Precision);
		Assert.Equal(0.0, output.GetAxisValue(Axis.Slider2), Precision);
	}

	// ── Complex modifier JSON round-trip through Runtime ────────────────

	[Fact]
	public void Json_ComplexNestedModifier_RoundTripsAndBehavesIdentically()
	{
		// AxisMapping.Modifier =
		//   WhenButtonPressedAxisModifier(Stateful = WhenPressed)
		//     WhenPressed     = AxisCurve(0.5)         ← halves input while held
		//     WhenNotPressed  = BlendedAxisCurve(Stateful = true)
		//         NormalCurve    = AxisCurve(1.0)      ← identity
		//         PrecisionCurve = AxisCurve(0.2)
		//         ModifierAxis   = Slider1 (unsigned)
		//
		// The whole thing is serialized to JSON, deserialized into a fresh
		// AppConfig, materialized through Runtime against a separate set of
		// fakes, and driven with the same inputs as the in-code version —
		// outputs must match frame-for-frame.

		var config = BuildComplexConfig(stickDeviceId: 1, outputDeviceId: 1u);

		var json = JsonSerializer.Serialize(config, typeof(AppConfig), AppJsonContext.Polymorphic);
		Assert.Contains("\"$type\": \"whenButtonPressed\"", json);
		Assert.Contains("\"$type\": \"blended\"", json);

		var roundTripped = (AppConfig?)JsonSerializer.Deserialize(json, typeof(AppConfig), AppJsonContext.Polymorphic);
		Assert.NotNull(roundTripped);

		using var origHarness = new ComplexHarness(config);
		using var rtHarness = new ComplexHarness(roundTripped!);

		void Drive(double x, double slider1, bool buttonPressed)
		{
			origHarness.Stick.SetAxisValue(Axis.X, x);
			origHarness.Stick.SetAxisValue(Axis.Slider1, slider1);
			origHarness.Stick.SetButtonState(1, buttonPressed);
			origHarness.Runtime.ProcessFrame();

			rtHarness.Stick.SetAxisValue(Axis.X, x);
			rtHarness.Stick.SetAxisValue(Axis.Slider1, slider1);
			rtHarness.Stick.SetButtonState(1, buttonPressed);
			rtHarness.Runtime.ProcessFrame();

			Assert.Equal(
				origHarness.Output.GetAxisValue(Axis.X),
				rtHarness.Output.GetAxisValue(Axis.X),
				Precision);
		}

		// 1. modifier slider at rest, no button → normal curve (identity).
		Drive(0.8, 0.0, buttonPressed: false);

		// 2. modifier slider engages fully → blended (stateful) holds value
		//    on entry, then drift through precision curve as input moves.
		Drive(0.8, 1.0, buttonPressed: false);
		Drive(0.6, 1.0, buttonPressed: false);

		// 3. press button → WhenPressed (stateful) holds previous output.
		Drive(0.6, 1.0, buttonPressed: true);

		// 4. drift input while button held → integrate through WhenPressed.
		Drive(0.4, 1.0, buttonPressed: true);

		// 5. release button → leaves stateful branch, blended takes over.
		Drive(0.4, 1.0, buttonPressed: false);

		// 6. modifier slider drops → blended fades back to normal curve.
		Drive(0.4, 0.5, buttonPressed: false);
		Drive(0.4, 0.0, buttonPressed: false);
	}

	private static AppConfig BuildComplexConfig(int stickDeviceId, uint outputDeviceId)
	{
		var modifier = new WhenButtonPressedAxisModifier
		{
			Buttons = [new(stickDeviceId, 1)],
			WhenPressed = new AxisCurve { Max = 0.5 },
			WhenNotPressed = new BlendedAxisCurve
			{
				NormalCurve = new AxisCurve { Max = 1.0 },
				PrecisionCurve = new AxisCurve { Max = 0.2 },
				ModifierAxis = new(stickDeviceId, Axis.Slider1, AxisMode.Unsigned),
				Stateful = true,
			},
			Stateful = WhenButtonPressedStateful.WhenPressed,
		};

		return new()
		{
			VJoyDeviceId = outputDeviceId,
			AxisMappings =
			{
				new()
				{
					Source = new() { DeviceId = stickDeviceId, Axis = "x" },
					TargetAxis = "x",
					Modifier = modifier,
				},
			},
		};
	}

	/// <summary>
	/// A self-contained fakes + runtime for one snapshot of an AppConfig.
	/// Two are created side-by-side in the round-trip test so the same input
	/// sequence can be driven through both.
	/// </summary>
	private sealed class ComplexHarness : IDisposable
	{
		public FakeDeviceManager Fakes { get; }
		public FakeJoystickDevice Stick { get; }
		public FakeOutputDevice Output { get; }
		public IFakesOutputRuntimeContext Runtime { get; }

		public ComplexHarness(AppConfig config)
		{
			Fakes = new();
			Stick = Fakes.AddInputDevice("Stick")
				.AddAxis(Axis.X)
				.AddAxis(Axis.Slider1)
				.AddButtons(4)
				.Build();
			Output = Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
			Runtime = FakesRuntime.Build(new()
			{
				Name = "test",
				ConnectedDevices = Fakes.InputDevices,
				OutputDeviceFactory = Fakes.OutputDeviceFactory,
				Routes = config.BuildRoutes(),
			});
		}

		public void Dispose()
		{
			Runtime.Dispose();
			Fakes.Dispose();
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────

	private static string Serialize(AppConfig config) =>
		JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);

	private static AppConfig? Deserialize(string json) =>
		JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);

	private IFakesOutputRuntimeContext BuildRuntime(AppConfig config) =>
		FakesRuntime.Build(new()
		{
			Name = config.Name ?? "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes = config.BuildRoutes(),
		});
}
