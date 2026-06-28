using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SharpSticks.InputAbstractions;

[JsonConverter(typeof(JsonStringEnumConverter<Axis>))]
public enum Axis
{
	X,
	Y,
	Z,
	Rx,
	Ry,
	Rz,
	Slider1,
	Slider2,
}

public static class AxisExtensions
{
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
	[OverloadResolutionPriority(2)]
	public static string ToStringFast(this Axis axis) => axis switch
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
	{
		Axis.X => nameof(Axis.X),
		Axis.Y => nameof(Axis.Y),
		Axis.Z => nameof(Axis.Z),
		Axis.Rx => nameof(Axis.Rx),
		Axis.Ry => nameof(Axis.Ry),
		Axis.Rz => nameof(Axis.Rz),
		Axis.Slider1 => nameof(Axis.Slider1),
		Axis.Slider2 => nameof(Axis.Slider2),
	};
}