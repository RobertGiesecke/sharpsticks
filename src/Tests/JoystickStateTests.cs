namespace SharpSticks.Tests;

/// <summary>
/// <see cref="JoystickState"/> packs 128 buttons into two ulong bit fields —
/// the 64/65 boundary is where an off-by-one would hide. Axis reads must map
/// each of the eight axes to its own slot.
/// </summary>
public sealed class JoystickStateTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();

	public void Dispose() => _Fakes.Dispose();

	[Fact]
	public void ButtonBits_SurviveTheUlongBoundary()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).AddButtons(128).Build();
		stick.PressButton(1);
		stick.PressButton(64);   // last bit of the first field
		stick.PressButton(65);   // first bit of the second field
		stick.PressButton(128);  // last bit of the second field
		Assert.True(stick.TryReadState(out var state, out _));

		Assert.True(state.IsButtonPressed(1));
		Assert.True(state.IsButtonPressed(64));
		Assert.True(state.IsButtonPressed(65));
		Assert.True(state.IsButtonPressed(128));

		Assert.False(state.IsButtonPressed(2));
		Assert.False(state.IsButtonPressed(63));
		Assert.False(state.IsButtonPressed(66));
		Assert.False(state.IsButtonPressed(127));
	}

	[Fact]
	public void EachAxis_ReadsItsOwnSlot()
	{
		var builder = _Fakes.AddInputDevice("Stick");
		Axis[] axes = [Axis.X, Axis.Y, Axis.Z, Axis.Rx, Axis.Ry, Axis.Rz, Axis.Slider1, Axis.Slider2];
		foreach (var axis in axes)
		{
			builder.AddAxis(axis);
		}

		var stick = builder.Build();
		for (var i = 0; i < axes.Length; i++)
		{
			stick.SetAxisValue(axes[i], (i + 1) * 0.1);
		}

		Assert.True(stick.TryReadState(out var state, out _));

		// Rising normalized inputs must come back as strictly rising raw slots —
		// any cross-wired axis pair would break the ordering.
		for (var i = 1; i < axes.Length; i++)
		{
			Assert.True(
				state.GetAxisValue(axes[i]) > state.GetAxisValue(axes[i - 1]),
				$"{axes[i]} should read above {axes[i - 1]}");
		}
	}
}
