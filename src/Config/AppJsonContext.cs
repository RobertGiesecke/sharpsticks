using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SharpSticks.Config;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ItbMinimalConfig))]
[JsonSerializable(typeof(IAxisModifier))]
[JsonSerializable(typeof(AxisCurve))]
[JsonSerializable(typeof(BlendedAxisCurve))]
[JsonSerializable(typeof(WhenButtonPressedAxisModifier))]
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
				},
			};
		}),
	};
}
