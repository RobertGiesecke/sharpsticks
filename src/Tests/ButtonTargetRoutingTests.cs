namespace SharpSticks.Tests;

/// <summary>
/// A source button routes to any <see cref="ButtonTarget"/> — vJoy button, mouse
/// button, or scroll increment — producing the matching discrete route type. The
/// concrete <c>OutputButtonBinding</c> overload stays more specific than the base one.
/// </summary>
public sealed class ButtonTargetRoutingTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly FakeJoystickDevice _Stick;

	public ButtonTargetRoutingTests()
	{
		_Stick = _Fakes.AddInputDevice("Stick").AddButtons(4).Build();
	}

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void RouteTo_BuildsRouteMatchingTargetKind()
	{
		var source = _Stick.BindButton(1);

		Assert.IsType<ButtonRoute>(source.RouteTo(new OutputButtonBinding(1, 1)));
		Assert.IsType<ButtonToMouseRoute>(source.RouteTo(new MouseButtonTarget { Button = OutputMouseButton.Left }));
		Assert.IsType<ButtonToScrollRoute>(source.RouteTo(ScrollTarget.Towards(ScrollDirection.Up)));
	}

	[Fact]
	public void RouteTo_ViaBaseReference_DispatchesToTarget()
	{
		var source = _Stick.BindButton(1);
		ButtonTarget vjoyButton = new OutputButtonBinding(1, 1);

		Assert.IsType<ButtonRoute>(source.RouteTo(vjoyButton));
	}

	[Fact]
	public void ScrollTarget_Towards_CarriesSignedAmountAndUnit()
	{
		var target = ScrollTarget.Towards(ScrollDirection.Down, magnitude: 3, unit: MouseScrollUnit.HighResolution);

		Assert.Equal(ScrollAxis.Vertical, target.Axis);
		Assert.Equal(-3, target.Amount);
		Assert.Equal(MouseScrollUnit.HighResolution, target.Unit);
	}
}
