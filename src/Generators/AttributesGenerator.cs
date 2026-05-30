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
		GeneratorLog.Log($"{nameof(AttributesGenerator)}.{nameof(Initialize)}");
		context.RegisterPostInitializationOutput(static postInitializationContext =>
		{
			GeneratorLog.Log($"{nameof(AttributesGenerator)}.PostInitialization: emit included-files resources");
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

				GeneratorLog.Log($"{nameof(AttributesGenerator)} AddSource: {resourceName} ({stream.Length} chars)");
				postInitializationContext.AddSource(resourceName, SourceText.From(stream, Encoding.UTF8, canBeEmbedded: true));
			}
		});
	}
}
