namespace SharpSticks.Tests;

using static FakeInputSynthesizer;

/// <summary>
/// Keyboard/mouse macro output: PressKey/ReleaseKey/PressMouseButton/
/// ReleaseMouseButton drive the injected <see cref="FakeInputSynthesizer"/>
/// directly (bypassing the vJoy output-button refcount path), in action order,
/// with the synthesizer flushed once per frame.
/// </summary>
public sealed class MacroSynthesizerTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;
	private readonly FakeTimeSource _Time = new();
	private readonly FakeInputSynthesizer _Synth = new();

	public MacroSynthesizerTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(4).Build();
		_Fakes.AddOutputDevice().AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void OnPress_SynthesizesKeyEvents_InActionOrder()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.PressKey(NamedKey.LeftControl),
				Macros.PressKey(NamedKey.C),
				Macros.ReleaseKey(NamedKey.C),
				Macros.ReleaseKey(NamedKey.LeftControl),
			],
		});

		runtime.ProcessFrame(); // baseline: establishes released edge, no events
		Assert.Empty(_Synth.Events);

		_Stick.PressButton(1);
		runtime.ProcessFrame(); // rising edge -> all four (no waits) run this frame

		Assert.Equal(
			new Event[]
			{
				new(EventKind.KeyDown, Key: NamedKey.LeftControl),
				new(EventKind.KeyDown, Key: NamedKey.C),
				new(EventKind.KeyUp, Key: NamedKey.C),
				new(EventKind.KeyUp, Key: NamedKey.LeftControl),
			},
			_Synth.Events);
	}

	[Fact]
	public void OnPress_SynthesizesMouseButtonEvents()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress =
			[
				Macros.PressMouseButton(OutputMouseButton.Left),
				Macros.ReleaseMouseButton(OutputMouseButton.Left),
			],
		});

		runtime.ProcessFrame();
		_Stick.PressButton(1);
		runtime.ProcessFrame();

		Assert.Equal(
			new Event[]
			{
				new(EventKind.MouseButtonDown, MouseButton: OutputMouseButton.Left),
				new(EventKind.MouseButtonUp, MouseButton: OutputMouseButton.Left),
			},
			_Synth.Events);
	}

	[Fact]
	public void Synthesizer_IsFlushedEveryFrame()
	{
		using var runtime = Build(new ButtonMacroRoute
		{
			Binding = _Stick.BindButton(1),
			OnPress = [Macros.PressKey(NamedKey.A)],
		});

		runtime.ProcessFrame();
		runtime.ProcessFrame();
		runtime.ProcessFrame();
		Assert.Equal(3, _Synth.FlushCount);
	}

	[Fact]
	public void KeyMacro_WithoutSynthesizer_FailsFastAtBuild()
	{
		var ex = Assert.Throws<InvalidOperationException>(() =>
			FakesRuntime.Build(new()
			{
				Name = "test",
				ConnectedDevices = _Fakes.InputDevices,
				OutputDeviceFactory = _Fakes.OutputDeviceFactory,
				TimeSource = _Time,
				Routes =
				[
					new ButtonMacroRoute
					{
						Binding = _Stick.BindButton(1),
						OnPress = [Macros.PressKey(NamedKey.A)],
					},
				],
				// no InputSynthesizer
			}));

		Assert.Contains("IInputSynthesizer", ex.Message);
	}

	private IFakesOutputRuntimeContext Build(params IBoundRoute[] routes) =>
		FakesRuntime.Build(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			OutputDeviceFactory = _Fakes.OutputDeviceFactory,
			TimeSource = _Time,
			InputSynthesizer = _Synth,
			Routes = [..routes],
		});
}
