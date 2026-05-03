namespace ScaledAxisCSharp.Config;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ItbMinimalConfig))]
public sealed partial class AppJsonContext : JsonSerializerContext
{
}