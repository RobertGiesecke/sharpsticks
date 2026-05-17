namespace SharpSticks.Tests;

public sealed class MergeAxesTests : IDisposable
{
	private const double Precision = 1e-9;

	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeOutputDevice _Output;

	public MergeAxesTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddAxis(Axis.Y).Build();
		_Output = _Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Theory]
	[InlineData(MergeMode.Sum, 0.3, 0.4, 0.7)]
	[InlineData(MergeMode.Sum, -0.5, 0.25, -0.25)]
	[InlineData(MergeMode.Average, 0.3, 0.5, 0.4)]
	[InlineData(MergeMode.Min, 0.3, 0.5, 0.3)]
	[InlineData(MergeMode.Min, -0.2, 0.5, -0.2)]
	[InlineData(MergeMode.Max, 0.3, 0.5, 0.5)]
	[InlineData(MergeMode.Max, -0.2, -0.5, -0.2)]
	[InlineData(MergeMode.Multiply, 0.5, 0.5, 0.25)]
	[InlineData(MergeMode.Multiply, -0.5, 0.5, -0.25)]
	public void Merge_CombinesTwoInputs(MergeMode mode, double xValue, double yValue, double expected)
	{
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			Mode = mode,
		});

		_Stick.SetAxisValue(Axis.X, xValue);
		_Stick.SetAxisValue(Axis.Y, yValue);
		runtime.ProcessFrame();

		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_DefaultModeIsSum()
	{
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
		});

		_Stick.SetAxisValue(Axis.X, 0.2);
		_Stick.SetAxisValue(Axis.Y, 0.3);
		runtime.ProcessFrame();

		Assert.Equal(0.5, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_AppliesPerSideScaleAndOffset()
	{
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			First = new() { Scale = 0.5, Offset = 0.1 },
			Second = new() { Scale = -1.0, Offset = 0.2 },
			Mode = MergeMode.Sum,
		});

		// first = 0.4 * 0.5 + 0.1 = 0.3
		// second = 0.6 * -1.0 + 0.2 = -0.4
		// sum = -0.1
		_Stick.SetAxisValue(Axis.X, 0.4);
		_Stick.SetAxisValue(Axis.Y, 0.6);
		runtime.ProcessFrame();

		Assert.Equal(-0.1, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_ComposesPerSideModifiers()
	{
		// First side gets a 0.5x curve, second side is identity.
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			First = new() { Modifier = new AxisCurve { Max = 0.5 } },
			Second = new(),
			Mode = MergeMode.Sum,
		});

		// first = 0.8 → 0.4 (via 0.5x curve)
		// second = 0.2
		// sum = 0.6
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Y, 0.2);
		runtime.ProcessFrame();

		Assert.Equal(0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_AcrossDevices_BothDevicesAreKeptAlive()
	{
		// Second axis lives on a separate input device; MergeAxesModifier.FillDevices
		// must register that device's id so RuntimeBuilder keeps it connected.
		using var fakes = new FakeDeviceManager();
		var first = fakes.AddInputDevice("First").AddAxis(Axis.X).Build();
		var second = fakes.AddInputDevice("Second").AddAxis(Axis.X).Build();
		var output = fakes.AddOutputDevice().AddAxis(Axis.X).Build();

		using var runtime = Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = fakes.InputDevices,
			OutputDeviceFactory = fakes.OutputDeviceFactory,
			Routes =
			[
				first.BindAxis(Axis.X).MergeWith(
					second.BindAxis(Axis.X),
					new()
					{
						OutputBinding = output.BindAxis(Axis.X),
						Mode = MergeMode.Average,
					}),
			],
		});

		first.SetAxisValue(Axis.X, 0.4);
		second.SetAxisValue(Axis.X, 0.8);
		runtime.ProcessFrame();

		Assert.Equal(0.6, output.GetAxisValue(Axis.X), Precision);
	}

	[Theory]
	[InlineData(MergeMode.Sum, 1.0, 0.5, -0.5)]
	[InlineData(MergeMode.Average, 1.0, 0.5, -0.25)]
	[InlineData(MergeMode.Sum, 0.5, 0.5, 0.0)]
	[InlineData(MergeMode.Sum, 0.0, 1.0, 1.0)]
	[InlineData(MergeMode.Sum, 1.0, 0.0, -1.0)]
	public void Merge_FirstInverted_MapsToNegativeHalf(MergeMode mode, double xValue, double yValue, double expected)
	{
		// Split-axis pedals: first axis [0,1] -> [-1,0]; second axis [0,1] -> [0,1].
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			First = new() { Invert = true },
			Mode = mode,
		});

		_Stick.SetAxisValue(Axis.X, xValue);
		_Stick.SetAxisValue(Axis.Y, yValue);
		runtime.ProcessFrame();

		Assert.Equal(expected, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_BothInverted_NegatesEachSide()
	{
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			First = new() { Invert = true },
			Second = new() { Invert = true },
			Mode = MergeMode.Sum,
		});

		// first = -0.4, second = -0.6, sum = -1.0
		_Stick.SetAxisValue(Axis.X, 0.4);
		_Stick.SetAxisValue(Axis.Y, 0.6);
		runtime.ProcessFrame();

		Assert.Equal(-1.0, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_InvertWithScaleAndOffset_NegatesBoth()
	{
		// Verifies invert folds into both Scale and Offset: output = -(Scale*x + Offset).
		using var runtime = BuildRuntime(new()
		{
			OutputBinding = _Output.BindAxis(Axis.X),
			First = new() { Scale = 0.5, Offset = 0.1, Invert = true },
			Mode = MergeMode.Sum,
		});

		// first  = -(0.5 * 0.8 + 0.1) = -0.5
		// second = 0
		_Stick.SetAxisValue(Axis.X, 0.8);
		_Stick.SetAxisValue(Axis.Y, 0.0);
		runtime.ProcessFrame();

		Assert.Equal(-0.5, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void RouteTo_InvertOption_NegatesPlainAxisRoute()
	{
		// Invert lives on RouteAxisOptions so it works for non-merge routes too.
		using var runtime = Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).RouteTo(_Output.BindAxis(Axis.X), new() { Invert = true }),
			],
		});

		_Stick.SetAxisValue(Axis.X, 0.6);
		runtime.ProcessFrame();
		Assert.Equal(-0.6, _Output.GetAxisValue(Axis.X), Precision);
	}

	[Fact]
	public void Merge_ProducesSingleAxisRoute()
	{
		var route = _Stick.BindAxis(Axis.X).MergeWith(
			_Stick.BindAxis(Axis.Y),
			new()
			{
				OutputBinding = _Output.BindAxis(Axis.X),
				Mode = MergeMode.Sum,
			});

		Assert.Equal(Axis.X, route.Source.Axis);
		Assert.Equal(Axis.X, route.OutputBinding.Axis);
		Assert.NotNull(route.Modifier);
	}

	private IOutputRuntimeContext BuildRuntime(MergeAxesOptions options) =>
		Runtime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			Routes =
			[
				_Stick.BindAxis(Axis.X).MergeWith(_Stick.BindAxis(Axis.Y), options),
			],
		});
}
