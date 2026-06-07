using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SharpSticks.Config;

// Metadata mode (no fast-path delegates): the generated fast-path serializers
// bypass TypeInfoResolver modifiers, silently dropping the IAxisModifier
// polymorphism for collection elements (they serialize as the bare interface
// contract — "{}").
[JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNameCaseInsensitive = true,
	GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ItbMinimalConfig))]
[JsonSerializable(typeof(IAxisModifier))]
[JsonSerializable(typeof(AxisCurve))]
[JsonSerializable(typeof(BlendedAxisCurve))]
[JsonSerializable(typeof(WhenButtonPressedAxisModifier))]
[JsonSerializable(typeof(AxisBinding))]
public sealed partial class AppJsonContext : JsonSerializerContext
{
	/// <summary>
	/// Source-gen options augmented with polymorphism for <see cref="IAxisModifier"/>.
	/// Use this when serializing/deserializing configs that may contain modifiers —
	/// the polymorphism config can't live as <c>[JsonDerivedType]</c> attributes on
	/// the interface because the derived types live in this assembly and the
	/// interface lives in <c>SharpSticks.InputAbstractions</c>.
	/// </summary>
	private static JsonSerializerOptions? _Polymorphic;

	public static JsonSerializerOptions Polymorphic => _Polymorphic ??= CreatePolymorphic();

	private static JsonSerializerOptions CreatePolymorphic() => new(Default.Options)
	{
		TypeInfoResolver = Default.WithAddedModifier(static info =>
		{
			if (info.Type != typeof(IAxisModifier))
			{
				return;
			}

			info.PolymorphismOptions = new()
			{
				TypeDiscriminatorPropertyName = "$type",
				DerivedTypes =
				{
					new(typeof(AxisCurve), "curve"),
					new(typeof(BlendedAxisCurve), "blended"),
					new(typeof(WhenButtonPressedAxisModifier), "whenButtonPressed"),
					// An AxisBinding used as a modifier reads the bound axis
					// (e.g. BlendedAxisCurve.ModifierAxis).
					new(typeof(AxisBinding), "axisValue"),
				},
			};
		}),
	};
}
