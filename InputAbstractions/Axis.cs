using System.Text.Json.Serialization;

namespace ScaledAxisCSharp.InputAbstractions;

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