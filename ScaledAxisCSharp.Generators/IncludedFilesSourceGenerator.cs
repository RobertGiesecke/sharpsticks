using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ScaledAxisCSharp.Generators;

[Generator]
public sealed class IncludedFilesSourceGenerator : IIncrementalGenerator
{
	private const string ResourcePrefix = "included-files/";
	private const string GenerateDeviceInfosAttributeMetadataName = "GenerateDeviceInfosAttribute";
	private const int GenerateDeviceInfosLevelsDeviceNames = 1;
	private const int GenerateDeviceInfosLevelsDeviceIds = 2;
	private const int GenerateDeviceInfosLevelsOutputDeviceIds = 4;
	private const int GenerateDeviceInfosLevelsTypedDevices = 8;
	private const int GenerateDeviceInfosLevelsDefault = GenerateDeviceInfosLevelsDeviceNames;

	private static readonly DiagnosticDescriptor TypeMustBePartial = new(
		"SACIG001",
		"GenerateDeviceInfos target must be partial",
		"Type '{0}' must be partial to receive generated device info members",
		"ScaledAxisCSharp.Generators",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor DirectInputUnavailable = new(
		"SACIG002",
		"DirectInput device snapshot unavailable",
		"DirectInput device info generation could not enumerate devices: {0}",
		"ScaledAxisCSharp.Generators",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor OutputDevicesUnavailable = new(
		"SACIG003",
		"Output device snapshot unavailable",
		"Output device info generation could not enumerate vJoy devices: {0}",
		"ScaledAxisCSharp.Generators",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static postInitializationContext =>
		{
			var assembly = typeof(IncludedFilesSourceGenerator).GetTypeInfo().Assembly;

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
				postInitializationContext.AddSource(GetHintName(resourceName), SourceText.From(source, Encoding.UTF8));
			}
		});

		var deviceInfoTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
				GenerateDeviceInfosAttributeMetadataName,
				static (node, _) => node is TypeDeclarationSyntax,
				static (syntaxContext, _) => new DeviceInfoTarget(
					(INamedTypeSymbol)syntaxContext.TargetSymbol,
					GetDeviceInfoLevels(syntaxContext)))
			.Where(static target => target.Type.TypeKind is TypeKind.Class or TypeKind.Struct);

		context.RegisterSourceOutput(
			deviceInfoTargets.Collect(),
			static (sourceProductionContext, targets) => GenerateDeviceInfos(sourceProductionContext, targets));
	}

	private static void GenerateDeviceInfos(SourceProductionContext context, ImmutableArray<DeviceInfoTarget> targets)
	{
		if (targets.IsDefaultOrEmpty)
		{
			return;
		}

		if (!DeviceSnapshots.TryEnumerateDirectInputDevices(out var directInputDevices, out var directInputError))
		{
			context.ReportDiagnostic(Diagnostic.Create(DirectInputUnavailable, Location.None, directInputError));
			directInputDevices = ImmutableArray<DirectInputDeviceSnapshot>.Empty;
		}

		if (!DeviceSnapshots.TryEnumerateOutputDevices(out var outputDevices, out var outputError))
		{
			context.ReportDiagnostic(Diagnostic.Create(OutputDevicesUnavailable, Location.None, outputError));
			outputDevices = ImmutableArray<VJoyDeviceSnapshot>.Empty;
		}

		foreach (var target in CoalesceTargets(targets))
		{
			if (!IsPartial(target.Type))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					TypeMustBePartial,
					target.Type.Locations.FirstOrDefault(static location => location.IsInSource),
					target.Type.ToDisplayString()));
				continue;
			}

			if (target.Levels == 0)
			{
				continue;
			}

			var source = GenerateDeviceInfosSource(target.Type, target.Levels, directInputDevices, outputDevices);
			context.AddSource(GetDeviceInfosHintName(target.Type), SourceText.From(source, Encoding.UTF8));
		}
	}

	private static string GenerateDeviceInfosSource(
		INamedTypeSymbol target,
		int levels,
		ImmutableArray<DirectInputDeviceSnapshot> directInputDevices,
		ImmutableArray<VJoyDeviceSnapshot> outputDevices)
	{
		var builder = new StringBuilder();
		var namespaceName = target.ContainingNamespace.IsGlobalNamespace
			? null
			: target.ContainingNamespace.ToDisplayString();
		var containingTypes = GetContainingTypeChain(target);

		builder.AppendLine("// <auto-generated />");
		builder.AppendLine("#nullable enable");
		builder.AppendLine();

		if (namespaceName is not null)
		{
			builder.Append("namespace ").Append(namespaceName).AppendLine(";");
			builder.AppendLine();
		}

		for (var index = 0; index < containingTypes.Length; index++)
		{
			var containingType = containingTypes[index];
			var indent = new string('\t', index);
			builder.Append(indent)
				.Append(GetAccessibility(containingType))
				.Append(' ');

			if (containingType.IsStatic)
			{
				builder.Append("static ");
			}

			builder.Append("partial ")
				.Append(GetTypeKind(containingType))
				.Append(' ')
				.Append(GetTypeDeclaration(containingType))
				.AppendLine();
			builder.Append(indent).AppendLine("{");
		}

		AppendDeviceMembers(builder, levels, directInputDevices, outputDevices, containingTypes.Length);

		for (var index = containingTypes.Length - 1; index >= 0; index--)
		{
			var indent = new string('\t', index);
			builder.Append(indent).AppendLine("}");
		}

		return builder.ToString();
	}

	private static void AppendDeviceMembers(
		StringBuilder builder,
		int levels,
		ImmutableArray<DirectInputDeviceSnapshot> directInputDevices,
		ImmutableArray<VJoyDeviceSnapshot> outputDevices,
		int indentLevel)
	{
		var directInputNames = GetDirectInputDeviceConstantNames(directInputDevices);
		var indent = new string('\t', indentLevel);
		var memberIndent = new string('\t', indentLevel + 1);
		var wroteMember = false;

		if (HasLevel(levels, GenerateDeviceInfosLevelsDeviceNames))
		{
			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public static class DeviceNames");
			builder.Append(indent).AppendLine("{");
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				builder.Append(memberIndent)
					.Append("public const string ")
					.Append(directInputNames[index])
					.Append(" = ")
					.Append(SymbolDisplay.FormatLiteral(directInputDevices[index].ProductName, quote: true))
					.AppendLine(";");
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevelsDeviceIds))
		{
			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public static class DeviceIds");
			builder.Append(indent).AppendLine("{");
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				builder.Append(memberIndent)
					.Append("public const int ")
					.Append(directInputNames[index])
					.Append(" = ")
					.Append(directInputDevices[index].DeviceId)
					.AppendLine(";");
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevelsOutputDeviceIds))
		{
			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public static class OutputDeviceIds");
			builder.Append(indent).AppendLine("{");
			foreach (var outputDevice in outputDevices.OrderBy(static device => device.DeviceId))
			{
				builder.Append(memberIndent)
					.Append("public const uint ")
					.Append("VJoyDevice")
					.Append(outputDevice.DeviceId)
					.Append(" = ")
					.Append(outputDevice.DeviceId)
					.AppendLine("u;");
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevelsTypedDevices))
		{
			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public readonly record struct DeviceInfo(string Name, int DeviceId)");
			builder.Append(indent).AppendLine("{");
			builder.Append(memberIndent)
				.AppendLine("public uint OutputDeviceId => checked((uint)DeviceId);");
			builder.Append(indent).AppendLine("}");
			builder.AppendLine();
			builder.Append(indent).AppendLine("public static class TypedDevices");
			builder.Append(indent).AppendLine("{");
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				builder.Append(memberIndent)
					.Append("public static DeviceInfo ")
					.Append(directInputNames[index])
					.Append(" { get; } = new(")
					.Append(SymbolDisplay.FormatLiteral(directInputDevices[index].ProductName, quote: true))
					.Append(", ")
					.Append(directInputDevices[index].DeviceId)
					.AppendLine(");");
			}

			builder.Append(indent).AppendLine("}");
		}
	}

	private static bool HasLevel(int levels, int level)
	{
		return (levels & level) == level;
	}

	private static void AppendSeparator(StringBuilder builder, ref bool wroteMember)
	{
		if (wroteMember)
		{
			builder.AppendLine();
		}

		wroteMember = true;
	}

	private static List<string> GetDirectInputDeviceConstantNames(ImmutableArray<DirectInputDeviceSnapshot> devices)
	{
		var baseNames = devices.Select(static device => ToIdentifier(device.ProductName)).ToArray();
		var counts = baseNames
			.GroupBy(static name => name, StringComparer.Ordinal)
			.ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
		var usedNames = new HashSet<string>(StringComparer.Ordinal);
		var names = new List<string>(devices.Length);

		for (var index = 0; index < devices.Length; index++)
		{
			var name = baseNames[index];
			if (counts[name] > 1)
			{
				name += devices[index].DeviceId;
			}

			var uniqueName = name;
			var suffix = 2;
			while (!usedNames.Add(uniqueName))
			{
				uniqueName = name + suffix;
				suffix++;
			}

			names.Add(uniqueName);
		}

		return names;
	}

	private static int GetDeviceInfoLevels(GeneratorAttributeSyntaxContext syntaxContext)
	{
		var attribute = syntaxContext.Attributes.FirstOrDefault(static data =>
			data.AttributeClass?.ToDisplayString() == GenerateDeviceInfosAttributeMetadataName);

		if (attribute is null)
		{
			return GenerateDeviceInfosLevelsDefault;
		}

		if (attribute.ConstructorArguments.Length > 0)
		{
			return GetTypedConstantInt32(attribute.ConstructorArguments[0], GenerateDeviceInfosLevelsDefault);
		}

		foreach (var namedArgument in attribute.NamedArguments)
		{
			if (namedArgument.Key == "Levels")
			{
				return GetTypedConstantInt32(namedArgument.Value, GenerateDeviceInfosLevelsDefault);
			}
		}

		return GenerateDeviceInfosLevelsDefault;
	}

	private static int GetTypedConstantInt32(TypedConstant value, int fallback)
	{
		if (value.Value is null)
		{
			return fallback;
		}

		return value.Value is int intValue
			? intValue
			: Convert.ToInt32(value.Value);
	}

	private static IEnumerable<DeviceInfoTarget> CoalesceTargets(ImmutableArray<DeviceInfoTarget> targets)
	{
		var byType = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);

		foreach (var target in targets)
		{
			byType[target.Type] = byType.TryGetValue(target.Type, out var existingLevels)
				? existingLevels | target.Levels
				: target.Levels;
		}

		foreach (var pair in byType)
		{
			yield return new DeviceInfoTarget(pair.Key, pair.Value);
		}
	}

	private static string ToIdentifier(string value)
	{
		var builder = new StringBuilder();
		var word = new StringBuilder();

		foreach (var character in value)
		{
			if (char.IsLetterOrDigit(character))
			{
				word.Append(character);
				continue;
			}

			AppendWord(builder, word);
		}

		AppendWord(builder, word);

		if (builder.Length == 0)
		{
			builder.Append("Device");
		}

		if (char.IsDigit(builder[0]))
		{
			builder.Insert(0, "Device");
		}

		var identifier = builder.ToString();
		return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
			? identifier
			: "Device" + identifier;
	}

	private static void AppendWord(StringBuilder builder, StringBuilder word)
	{
		if (word.Length == 0)
		{
			return;
		}

		var allUpper = true;
		for (var index = 0; index < word.Length; index++)
		{
			if (char.IsLetter(word[index]) && char.IsLower(word[index]))
			{
				allUpper = false;
				break;
			}
		}

		builder.Append(char.ToUpperInvariant(word[0]));
		for (var index = 1; index < word.Length; index++)
		{
			builder.Append(allUpper ? char.ToLowerInvariant(word[index]) : word[index]);
		}

		word.Clear();
	}

	private static ImmutableArray<INamedTypeSymbol> GetContainingTypeChain(INamedTypeSymbol target)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		for (INamedTypeSymbol? current = target; current is not null; current = current.ContainingType)
		{
			builder.Add(current);
		}

		builder.Reverse();
		return builder.ToImmutable();
	}

	private static bool IsPartial(INamedTypeSymbol target)
	{
		return target.DeclaringSyntaxReferences
			.Select(static syntaxReference => syntaxReference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}

	private static string GetDeviceInfosHintName(INamedTypeSymbol target)
	{
		var displayName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var builder = new StringBuilder(displayName.Length);

		foreach (var character in displayName)
		{
			builder.Append(char.IsLetterOrDigit(character) ? character : '_');
		}

		return builder + ".DeviceInfos.g.cs";
	}

	private static string GetAccessibility(INamedTypeSymbol symbol)
	{
		return symbol.DeclaredAccessibility switch
		{
			Accessibility.Public => "public",
			Accessibility.Protected => "protected",
			Accessibility.Internal => "internal",
			Accessibility.ProtectedOrInternal => "protected internal",
			Accessibility.ProtectedAndInternal => "private protected",
			Accessibility.Private => "private",
			_ => "internal",
		};
	}

	private static string GetTypeKind(INamedTypeSymbol symbol)
	{
		if (symbol.IsRecord)
		{
			return symbol.TypeKind == TypeKind.Struct ? "record struct" : "record";
		}

		return symbol.TypeKind == TypeKind.Struct ? "struct" : "class";
	}

	private static string GetTypeDeclaration(INamedTypeSymbol symbol)
	{
		if (symbol.TypeParameters.Length == 0)
		{
			return symbol.Name;
		}

		return symbol.Name + "<" + string.Join(", ", symbol.TypeParameters.Select(static parameter => parameter.Name)) +
		       ">";
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
		foreach (var character in value)
		{
			hash ^= character;
			hash *= prime;
		}

		return hash;
	}

	private readonly struct DeviceInfoTarget
	{
		public DeviceInfoTarget(INamedTypeSymbol type, int levels)
		{
			Type = type;
			Levels = levels;
		}

		public INamedTypeSymbol Type { get; }
		public int Levels { get; }
	}
}
