using System.Text.Json.Serialization;

namespace ScaledAxisCSharp.InputAbstractions;

[JsonConverter(typeof(JsonStringEnumConverter<PhysicalAxis>))]
public enum PhysicalAxis
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