using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SharpSticks.Generators;

/// Emits the user-facing attribute definitions (GenerateDeviceInfosAttribute,
/// RenameDeviceAttribute, etc.) from embedded resources. Uses only
/// RegisterPostInitializationOutput, which runs once at compilation start and
/// is not subject to the cancellation/partial-emission behaviour the device
/// pipeline can hit — so the attributes are always in scope for the user's
/// source, even when the device pipeline gets cancelled mid-run.
[Generator]
public sealed class AttributesGenerator : IIncrementalGenerator
{
	private const string ResourcePrefix = "included-files/";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		GeneratorLog.Log("AttributesGenerator.Initialize");
		context.RegisterPostInitializationOutput(static postInitializationContext =>
		{
			GeneratorLog.Log("AttributesGenerator.PostInitialization: emit included-files resources");
			var assembly = typeof(AttributesGenerator).GetTypeInfo().Assembly;

			foreach (var resourceName in assembly.GetManifestResourceNames()
				         .OrderBy(static name => name, StringComparer.Ordinal))
			{
				if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
				    !resourceName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				using var stream = assembly.GetManifestResourceStream(resourceName);
				if (stream is null)
				{
					continue;
				}

				using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
				var source = reader.ReadToEnd();
				var hintName = GetHintName(resourceName);
				GeneratorLog.Log($"AttributesGenerator AddSource: {hintName} ({source.Length} chars)");
				postInitializationContext.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
			}
		});
	}

	private static string GetHintName(string resourceName)
	{
		var relativePath = resourceName.Substring(ResourcePrefix.Length).Replace('\\', '/');
		var hash = CalculateFnv1A(relativePath);

		return "IncludedFiles/" + relativePath + "." + hash.ToString("x8") + ".g.cs";
	}

	private static uint CalculateFnv1A(string value)
	{
		const uint offsetBasis = 2166136261;
		const uint prime = 16777619;

		var hash = offsetBasis;
		foreach (var c in value)
		{
			hash ^= c;
			hash *= prime;
		}

		return hash;
	}
}
