using System.Text.Json.Serialization;

namespace ScaledAxisCSharp.Config;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ItbMinimalConfig))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}