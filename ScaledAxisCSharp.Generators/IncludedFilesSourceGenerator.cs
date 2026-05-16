using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.Generators;

[Generator]
public sealed class IncludedFilesSourceGenerator : IIncrementalGenerator
{
	private const string ResourcePrefix = "included-files/";
	private const string GenerateDeviceInfosAttributeMetadataName = "GenerateDeviceInfosAttribute";
	private const string RenameDeviceAttributeMetadataName = "RenameDeviceAttribute";
	private const string RenameAxisAttributeMetadataName = "RenameAxis";
	private const string RenameButtonAttributeMetadataName = "RenameButton";
	private const GenerateDeviceInfosLevels GenerateDeviceInfosLevelsDefault = GenerateDeviceInfosLevels.DeviceNames;

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
					GetDeviceInfoLevels(syntaxContext),
					GetDeviceRenames(syntaxContext),
					GetAxisRenames(syntaxContext),
					GetButtonRenames(syntaxContext)))
			.Where(static target => target.Type.TypeKind is TypeKind.Class or TypeKind.Struct);

		context.RegisterSourceOutput(
			deviceInfoTargets.Collect(),
			static (sourceProductionContext, targets) => GenerateDeviceInfos(sourceProductionContext, targets));

		var assemblyTarget = context.CompilationProvider.Select(static (compilation, _) =>
		{
			var attributes = compilation.Assembly.GetAttributes();
			var generateAttr = attributes.FirstOrDefault(static data =>
				data.AttributeClass?.ToDisplayString() == GenerateDeviceInfosAttributeMetadataName);
			if (generateAttr is null)
			{
				return default;
			}

			var classAttributes = compilation.GetTypeByMetadataName("Devices")?.GetAttributes()
			                      ?? ImmutableArray<AttributeData>.Empty;

			return new AssemblyDeviceInfoTarget(
				GetDeviceInfoLevels(attributes),
				GetDeviceRenames(attributes).AddRange(GetDeviceRenames(classAttributes)),
				GetAxisRenames(attributes).AddRange(GetAxisRenames(classAttributes)),
				GetButtonRenames(attributes).AddRange(GetButtonRenames(classAttributes)));
		});

		context.RegisterSourceOutput(
			assemblyTarget,
			static (sourceProductionContext, target) => GenerateAssemblyDeviceInfos(sourceProductionContext, target));
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

			var source = GenerateDeviceInfosSource(
				target.Type, target.Levels,
				target.DeviceRenames, target.AxisRenames, target.ButtonRenames,
				directInputDevices, outputDevices);
			context.AddSource(GetDeviceInfosHintName(target.Type), SourceText.From(source, Encoding.UTF8));

			var fqn = target.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			context.AddSource(GetDeviceInfosHintName(target.Type).Replace(".DeviceInfos.g.cs", ".GlobalUsings.g.cs"),
				SourceText.From(BuildGlobalUsings(fqn, target.Levels), Encoding.UTF8));
		}
	}

	private static string BuildGlobalUsings(string qualifiedTypeName, GenerateDeviceInfosLevels levels)
	{
		var usings = new StringBuilder();
		usings.AppendLine("// <auto-generated />");
		usings.Append("global using static ").Append(qualifiedTypeName).AppendLine(";");
		if (HasLevel(levels, GenerateDeviceInfosLevels.TypedDevices))
		{
			usings.Append("global using static ").Append(qualifiedTypeName).AppendLine(".Typed;");
		}

		return usings.ToString();
	}

	private static void GenerateAssemblyDeviceInfos(SourceProductionContext context, AssemblyDeviceInfoTarget target)
	{
		if (!target.IsPresent || target.Levels == 0)
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

		var builder = new StringBuilder();
		builder.AppendLine("// <auto-generated />");
		builder.AppendLine("#nullable enable");
		if (HasLevel(target.Levels, GenerateDeviceInfosLevels.TypedDevices))
		{
			builder.AppendLine("using System.Collections.Immutable;");
		}

		builder.AppendLine();
		builder.AppendLine("internal partial class Devices");
		builder.AppendLine("{");

		AppendDeviceMembers(builder, target.Levels, target.DeviceRenames, target.AxisRenames, target.ButtonRenames,
			directInputDevices, outputDevices, indentLevel: 1);

		builder.AppendLine("}");

		context.AddSource("Devices.AssemblyDeviceInfos.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));

		context.AddSource("Devices.GlobalUsings.g.cs",
			SourceText.From(BuildGlobalUsings("Devices", target.Levels), Encoding.UTF8));
	}

	private static string GenerateDeviceInfosSource(
		INamedTypeSymbol target,
		GenerateDeviceInfosLevels levels,
		ImmutableArray<DeviceRename> deviceRenames,
		ImmutableArray<AxisRename> axisRenames,
		ImmutableArray<ButtonRename> buttonRenames,
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
		if (HasLevel(levels, GenerateDeviceInfosLevels.TypedDevices))
		{
			builder.AppendLine("using System.Collections.Immutable;");
		}

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

		AppendDeviceMembers(builder, levels, deviceRenames, axisRenames, buttonRenames, directInputDevices,
			outputDevices,
			containingTypes.Length);

		for (var index = containingTypes.Length - 1; index >= 0; index--)
		{
			var indent = new string('\t', index);
			builder.Append(indent).AppendLine("}");
		}

		return builder.ToString();
	}

	private static void AppendDeviceMembers(
		StringBuilder builder,
		GenerateDeviceInfosLevels levels,
		ImmutableArray<DeviceRename> deviceRenames,
		ImmutableArray<AxisRename> axisRenames,
		ImmutableArray<ButtonRename> buttonRenames,
		ImmutableArray<DirectInputDeviceSnapshot> directInputDevices,
		ImmutableArray<VJoyDeviceSnapshot> outputDevices,
		int indentLevel)
	{
		var directInputNames = GetDirectInputDeviceConstantNames(directInputDevices, outputDevices);
		var deviceIdentifiers = GetDeviceIdentifiers(directInputDevices, directInputNames, deviceRenames);
		var originalNameSet = new HashSet<string>(directInputNames, StringComparer.Ordinal);
		var outputDeviceNameSet = new HashSet<string>(
			outputDevices.Select(static d => $"VJoyDevice{d.DeviceId}"),
			StringComparer.Ordinal);
		var indent = new string('\t', indentLevel);
		var memberIndent = new string('\t', indentLevel + 1);
		var wroteMember = false;

		if (HasLevel(levels, GenerateDeviceInfosLevels.DeviceNames))
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

			for (var index = 0; index < directInputDevices.Length; index++)
			{
				var alias = deviceIdentifiers[index];
				if (alias != directInputNames[index] && !originalNameSet.Contains(alias))
				{
					builder.Append(memberIndent)
						.Append("public const string ")
						.Append(alias)
						.Append(" = ")
						.Append(directInputNames[index])
						.AppendLine(";");
				}
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevels.DeviceIds))
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

			for (var index = 0; index < directInputDevices.Length; index++)
			{
				var alias = deviceIdentifiers[index];
				if (alias != directInputNames[index] && !originalNameSet.Contains(alias))
				{
					builder.Append(memberIndent)
						.Append("public const int ")
						.Append(alias)
						.Append(" = ")
						.Append(directInputNames[index])
						.AppendLine(";");
				}
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevels.OutputDeviceIds))
		{
			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public static class OutputDeviceIds");
			builder.Append(indent).AppendLine("{");
			foreach (var outputDevice in outputDevices.OrderBy(static device => device.DeviceId))
			{
				var baseName = $"VJoyDevice{outputDevice.DeviceId}";
				builder.Append(memberIndent)
					.Append("public const uint ")
					.Append(baseName)
					.Append(" = ")
					.Append(outputDevice.DeviceId)
					.AppendLine("u;");
			}

			foreach (var outputDevice in outputDevices.OrderBy(static device => device.DeviceId))
			{
				var baseName = $"VJoyDevice{outputDevice.DeviceId}";
				var alias = GetOutputDeviceIdentifier(baseName, deviceRenames);
				if (alias != baseName)
				{
					builder.Append(memberIndent)
						.Append("public const uint ")
						.Append(alias)
						.Append(" = ")
						.Append(baseName)
						.AppendLine(";");
				}
			}

			builder.Append(indent).AppendLine("}");
		}

		if (HasLevel(levels, GenerateDeviceInfosLevels.TypedDevices))
		{
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				if (outputDeviceNameSet.Contains(directInputNames[index]))
				{
					continue;
				}

				var axisPropertyNames = BuildAxisPropertyNames(directInputDevices[index].ProductName,
					directInputNames[index], axisRenames);
				var buttonPropertyNames = BuildButtonPropertyNames(directInputDevices[index].ProductName,
					directInputNames[index], buttonRenames);
				AppendSeparator(builder, ref wroteMember);
				AppendTypedDeviceClass(
					builder,
					deviceIdentifiers[index],
					directInputNames[index],
					directInputDevices[index],
					axisPropertyNames,
					buttonPropertyNames,
					indent,
					memberIndent);
			}

			foreach (var outputDevice in outputDevices.OrderBy(static d => d.DeviceId))
			{
				var vjoyBaseName = $"VJoyDevice{outputDevice.DeviceId}";
				var vjoyIdentifier = GetOutputDeviceIdentifier(vjoyBaseName, deviceRenames);
				var diIdx = directInputNames.IndexOf(vjoyBaseName);
				var deviceName = diIdx >= 0 ? directInputDevices[diIdx].ProductName : vjoyBaseName;
				var outputAxisNames = BuildAxisPropertyNames(deviceName, vjoyBaseName, axisRenames, vjoyIdentifier);
				var outputButtonNames =
					BuildButtonPropertyNames(deviceName, vjoyBaseName, buttonRenames, vjoyIdentifier);
				AppendSeparator(builder, ref wroteMember);
				AppendTypedOutputDeviceClass(builder, vjoyIdentifier, vjoyBaseName, outputDevice.Axes, outputAxisNames,
					outputDevice.ButtonCount, outputButtonNames, indent, memberIndent);
			}

			AppendSeparator(builder, ref wroteMember);
			builder.Append(indent).AppendLine("public static class Typed");
			builder.Append(indent).AppendLine("{");
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				if (outputDeviceNameSet.Contains(directInputNames[index]))
				{
					continue;
				}

				builder.Append(memberIndent)
					.Append("/// <summary>")
					.Append(directInputDevices[index].ProductName)
					.AppendLine("</summary>");
				builder.Append(memberIndent)
					.Append("public static Typed")
					.Append(deviceIdentifiers[index])
					.Append(' ')
					.Append(deviceIdentifiers[index])
					.AppendLine(" { get; } = new();");
			}

			foreach (var outputDevice in outputDevices.OrderBy(static d => d.DeviceId))
			{
				var vjoyBaseName = $"VJoyDevice{outputDevice.DeviceId}";
				var vjoyIdentifier = GetOutputDeviceIdentifier(vjoyBaseName, deviceRenames);
				var diIdx = directInputNames.IndexOf(vjoyBaseName);
				var deviceName = diIdx >= 0 ? directInputDevices[diIdx].ProductName : vjoyBaseName;
				builder.Append(memberIndent)
					.Append("/// <summary>")
					.Append(deviceName)
					.AppendLine("</summary>");
				builder.Append(memberIndent)
					.Append("public static Typed")
					.Append(vjoyIdentifier)
					.Append(' ')
					.Append(vjoyIdentifier)
					.AppendLine(" { get; } = new();");
			}

			builder.Append(indent).AppendLine("}");
		}
	}

	private static void AppendTypedDeviceClass(
		StringBuilder builder,
		string deviceIdentifier,
		string deviceOriginalName,
		DirectInputDeviceSnapshot device,
		Dictionary<Axis, string> axisPropertyNames,
		Dictionary<int, string> buttonPropertyNames,
		string indent,
		string memberIndent)
	{
		var classIndent = indent;
		var innerIndent = memberIndent;
		var innerMemberIndent = memberIndent + "\t";

		builder.Append(classIndent).Append("public sealed record Typed").AppendLine(deviceIdentifier);
		builder.Append(classIndent).AppendLine("{");

		builder.Append(innerIndent)
			.Append("public const string DeviceName = DeviceNames.")
			.Append(deviceOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent)
			.Append("public const int DeviceId = DeviceIds.")
			.Append(deviceOriginalName)
			.AppendLine(";");

		builder.Append(innerIndent).AppendLine("public TypedAxisBindings Axes { get; } = new();");
		builder.Append(innerIndent).AppendLine("public TypedButtonBindings Buttons { get; } = new();");

		builder.AppendLine();
		builder.Append(innerIndent).AppendLine("public sealed record TypedAxisBindings");
		builder.Append(innerIndent).AppendLine("{");

		foreach (var axisName in device.Axes)
		{
			var propName = AxisPropertyName(axisName, axisPropertyNames);
			if (propName != axisName.ToString())
			{
				builder.Append(innerMemberIndent).Append("/// <summary>").Append(axisName).AppendLine("</summary>");
			}

			builder.Append(innerMemberIndent)
				.Append("public AxisBinding ")
				.Append(propName)
				.Append(" { get; } = new(DeviceId, Axis.")
				.Append(axisName)
				.AppendLine(");");
		}

		builder.Append(innerMemberIndent).AppendLine("public ImmutableArray<AxisBinding> All { get; }");
		builder.AppendLine();
		builder.Append(innerMemberIndent).AppendLine("public TypedAxisBindings()");
		builder.Append(innerMemberIndent).AppendLine("{");
		builder.Append(innerMemberIndent).Append("\tAll = [");
		builder.Append(string.Join(", ", device.Axes.Select(a => AxisPropertyName(a, axisPropertyNames))));
		builder.AppendLine("];");
		builder.Append(innerMemberIndent).AppendLine("}");

		if (device.Axes.Length > 0)
		{
			builder.AppendLine();
			builder.Append(innerMemberIndent).AppendLine("public AxisBinding this[Axis axis] => axis switch");
			builder.Append(innerMemberIndent).AppendLine("{");
			foreach (var axisName in device.Axes)
			{
				var propName = AxisPropertyName(axisName, axisPropertyNames);
				builder.Append(innerMemberIndent).Append("\tAxis.")
					.Append(axisName).Append(" => ").Append(propName).AppendLine(",");
			}

			builder.Append(innerMemberIndent)
				.AppendLine(
					"\t_ => throw new ArgumentOutOfRangeException(nameof(axis), $\"Axis {axis} is not defined for {DeviceName}.\")");
			builder.Append(innerMemberIndent).AppendLine("};");
		}

		builder.Append(innerIndent).AppendLine("}");

		if (device.ButtonCount > 0)
		{
			builder.AppendLine();
			builder.Append(innerIndent).AppendLine("public sealed record TypedButtonBindings");
			builder.Append(innerIndent).AppendLine("{");

			for (var btn = 1; btn <= device.ButtonCount; btn++)
			{
				var propName = buttonPropertyNames.GetValueOrDefault(btn, $"Btn{btn}");
				if (buttonPropertyNames.ContainsKey(btn))
				{
					builder.Append(innerMemberIndent).Append("/// <summary>Button ").Append(btn)
						.AppendLine("</summary>");
				}

				builder.Append(innerMemberIndent)
					.Append("public ButtonBinding ")
					.Append(propName)
					.Append(" { get; } = new(DeviceId, ")
					.Append(btn)
					.AppendLine(");");
			}

			builder.Append(innerMemberIndent).AppendLine("public ImmutableArray<ButtonBinding> All { get; }");
			builder.AppendLine();
			builder.Append(innerMemberIndent).AppendLine("public TypedButtonBindings()");
			builder.Append(innerMemberIndent).AppendLine("{");
			builder.Append(innerMemberIndent).Append("\tAll = [");
			builder.Append(string.Join(", ",
				Enumerable.Range(1, (int)device.ButtonCount)
					.Select(b => buttonPropertyNames.GetValueOrDefault(b, $"Btn{b}"))));
			builder.AppendLine("];");
			builder.Append(innerMemberIndent).AppendLine("}");
			builder.Append(innerIndent).AppendLine("}");
		}
		else
		{
			builder.AppendLine();
			builder.Append(innerIndent).AppendLine("public sealed record TypedButtonBindings");
			builder.Append(innerIndent).AppendLine("{");
			builder.Append(innerMemberIndent).AppendLine("public ImmutableArray<ButtonBinding> All { get; } = [];");
			builder.Append(innerIndent).AppendLine("}");
		}

		builder.Append(classIndent).AppendLine("}");
	}

	private static void AppendTypedOutputDeviceClass(
		StringBuilder builder,
		string vjoyIdentifier,
		string vjoyOriginalName,
		ImmutableArray<Axis> axes,
		Dictionary<Axis, string> axisPropertyNames,
		uint buttonCount,
		Dictionary<int, string> buttonPropertyNames,
		string indent,
		string memberIndent)
	{
		var innerIndent = memberIndent;
		var innerMemberIndent = memberIndent + "\t";

		builder.Append(indent).Append("public sealed record Typed").AppendLine(vjoyIdentifier);
		builder.Append(indent).AppendLine("{");

		builder.Append(innerIndent).Append("public const string DeviceName = DeviceNames.").Append(vjoyOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent).Append("public const uint DeviceId = OutputDeviceIds.").Append(vjoyOriginalName)
			.AppendLine(";");

		builder.Append(innerIndent).AppendLine("public TypedAxisBindings Axes { get; } = new();");
		builder.Append(innerIndent).AppendLine("public TypedButtonBindings Buttons { get; } = new();");

		builder.AppendLine();
		builder.Append(innerIndent).AppendLine("public sealed record TypedAxisBindings");
		builder.Append(innerIndent).AppendLine("{");

		foreach (var axis in axes)
		{
			var propName = AxisPropertyName(axis, axisPropertyNames);
			if (propName != axis.ToString())
			{
				builder.Append(innerMemberIndent).Append("/// <summary>").Append(axis).AppendLine("</summary>");
			}

			builder.Append(innerMemberIndent)
				.Append("public OutputAxisBinding ")
				.Append(propName)
				.Append(" { get; } = new(DeviceId, Axis.")
				.Append(axis)
				.AppendLine(");");
		}

		builder.Append(innerMemberIndent).AppendLine("public ImmutableArray<OutputAxisBinding> All { get; }");
		builder.AppendLine();
		builder.Append(innerMemberIndent).AppendLine("public TypedAxisBindings()");
		builder.Append(innerMemberIndent).AppendLine("{");
		builder.Append(innerMemberIndent).Append("\tAll = [");
		builder.Append(string.Join(", ", axes.Select(a => AxisPropertyName(a, axisPropertyNames))));
		builder.AppendLine("];");
		builder.Append(innerMemberIndent).AppendLine("}");

		if (axes.Length > 0)
		{
			builder.AppendLine();
			builder.Append(innerMemberIndent).AppendLine("public OutputAxisBinding this[Axis axis] => axis switch");
			builder.Append(innerMemberIndent).AppendLine("{");
			foreach (var axis in axes)
			{
				builder.Append(innerMemberIndent).Append("\tAxis.")
					.Append(axis).Append(" => ").Append(AxisPropertyName(axis, axisPropertyNames)).AppendLine(",");
			}

			builder.Append(innerMemberIndent)
				.AppendLine(
					"\t_ => throw new ArgumentOutOfRangeException(nameof(axis), $\"Axis {axis} is not defined for {DeviceName}.\")");
			builder.Append(innerMemberIndent).AppendLine("};");
		}

		builder.Append(innerIndent).AppendLine("}");

		if (buttonCount > 0)
		{
			builder.AppendLine();
			builder.Append(innerIndent).AppendLine("public sealed record TypedButtonBindings");
			builder.Append(innerIndent).AppendLine("{");

			for (var btn = 1; btn <= buttonCount; btn++)
			{
				var propName = buttonPropertyNames.GetValueOrDefault(btn, $"Btn{btn}");
				if (buttonPropertyNames.ContainsKey(btn))
				{
					builder.Append(innerMemberIndent).Append("/// <summary>Button ").Append(btn)
						.AppendLine("</summary>");
				}

				builder.Append(innerMemberIndent)
					.Append("public OutputButtonBinding ")
					.Append(propName)
					.Append(" { get; } = new(DeviceId, ")
					.Append(btn)
					.AppendLine(");");
			}

			builder.Append(innerMemberIndent).AppendLine("public ImmutableArray<OutputButtonBinding> All { get; }");
			builder.AppendLine();
			builder.Append(innerMemberIndent).AppendLine("public TypedButtonBindings()");
			builder.Append(innerMemberIndent).AppendLine("{");
			builder.Append(innerMemberIndent).Append("\tAll = [");
			builder.Append(string.Join(", ",
				Enumerable.Range(1, (int)buttonCount)
					.Select(b => buttonPropertyNames.GetValueOrDefault(b, $"Btn{b}"))));
			builder.AppendLine("];");
			builder.Append(innerMemberIndent).AppendLine("}");
			builder.Append(innerIndent).AppendLine("}");
		}
		else
		{
			builder.AppendLine();
			builder.Append(innerIndent).AppendLine("public sealed record TypedButtonBindings");
			builder.Append(innerIndent).AppendLine("{");
			builder.Append(innerMemberIndent)
				.AppendLine("public ImmutableArray<OutputButtonBinding> All { get; } = [];");
			builder.Append(innerIndent).AppendLine("}");
		}

		builder.Append(indent).AppendLine("}");
	}

	private static string[] GetDeviceIdentifiers(
		ImmutableArray<DirectInputDeviceSnapshot> devices,
		List<string> baseNames,
		ImmutableArray<DeviceRename> deviceRenames)
	{
		var identifiers = new string[devices.Length];
		for (var i = 0; i < devices.Length; i++)
		{
			var rename = deviceRenames.FirstOrDefault(r =>
				// ReSharper disable AccessToModifiedClosure
				r.DeviceName == devices[i].ProductName || r.DeviceName == baseNames[i]);
			// ReSharper restore AccessToModifiedClosure
			identifiers[i] = rename.DeviceName is not null ? rename.NewName : baseNames[i];
		}

		return identifiers;
	}

	private static string GetOutputDeviceIdentifier(string baseName, ImmutableArray<DeviceRename> deviceRenames)
	{
		foreach (var rename in deviceRenames)
		{
			if (rename.DeviceName == baseName)
			{
				return rename.NewName;
			}
		}

		return baseName;
	}

	private static Dictionary<Axis, string> BuildAxisPropertyNames(
		string deviceProductName,
		string deviceBaseIdentifier,
		ImmutableArray<AxisRename> axisRenames,
		string? extraIdentifier = null)
	{
		var result = new Dictionary<Axis, string>();
		foreach (var rename in axisRenames)
		{
			if (rename.DeviceName == deviceProductName || rename.DeviceName == deviceBaseIdentifier
			                                           || (extraIdentifier != null &&
			                                               rename.DeviceName == extraIdentifier))
			{
				result[rename.OriginalAxis] = rename.NewPropertyName;
			}
		}

		return result;
	}

	private static string AxisPropertyName(Axis axisName, Dictionary<Axis, string> axisPropertyNames) =>
		axisPropertyNames.GetValueOrDefault(axisName, axisName.ToString());

	private static Dictionary<int, string> BuildButtonPropertyNames(
		string deviceProductName,
		string deviceBaseIdentifier,
		ImmutableArray<ButtonRename> buttonRenames,
		string? extraIdentifier = null)
	{
		var result = new Dictionary<int, string>();
		foreach (var rename in buttonRenames)
		{
			if (rename.DeviceName == deviceProductName || rename.DeviceName == deviceBaseIdentifier
			                                           || (extraIdentifier != null &&
			                                               rename.DeviceName == extraIdentifier))
			{
				result[rename.Button] = rename.NewPropertyName;
			}
		}

		return result;
	}

	private static ImmutableArray<ButtonRename> GetButtonRenames(GeneratorAttributeSyntaxContext syntaxContext) =>
		GetButtonRenames(syntaxContext.TargetSymbol.GetAttributes());

	private static ImmutableArray<ButtonRename> GetButtonRenames(IEnumerable<AttributeData> attributes)
	{
		var builder = ImmutableArray.CreateBuilder<ButtonRename>();
		foreach (var attr in attributes)
		{
			if (attr.AttributeClass?.ToDisplayString() != RenameButtonAttributeMetadataName)
			{
				continue;
			}

			if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax { ArgumentList: { } argList }
			    || argList.Arguments.Count != 3)
			{
				continue;
			}

			var deviceName = GetStringFromExpression(argList.Arguments[0].Expression, attr, 0);
			if (deviceName is null)
			{
				continue;
			}

			var buttonNumber = attr.ConstructorArguments.Length > 1
				? GetTypedConstantInt32(attr.ConstructorArguments[1], -1)
				: GetInt32FromExpression(argList.Arguments[1].Expression);
			if (buttonNumber < 1)
			{
				continue;
			}

			var newName = GetStringFromExpression(argList.Arguments[2].Expression, attr, 2);
			if (newName is null)
			{
				continue;
			}

			builder.Add(new(deviceName, buttonNumber, newName));
		}

		return builder.ToImmutable();
	}

	private static int GetInt32FromExpression(ExpressionSyntax expr)
	{
		if (expr is LiteralExpressionSyntax { Token.Value: int i })
		{
			return i;
		}

		return -1;
	}

	private static ImmutableArray<DeviceRename> GetDeviceRenames(GeneratorAttributeSyntaxContext syntaxContext) =>
		GetDeviceRenames(syntaxContext.TargetSymbol.GetAttributes());

	private static ImmutableArray<DeviceRename> GetDeviceRenames(IEnumerable<AttributeData> attributes)
	{
		var builder = ImmutableArray.CreateBuilder<DeviceRename>();
		foreach (var attr in attributes)
		{
			if (attr.AttributeClass?.ToDisplayString() != RenameDeviceAttributeMetadataName)
			{
				continue;
			}

			if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax { ArgumentList: { } argList }
			    || argList.Arguments.Count != 2)
			{
				continue;
			}

			var deviceName = GetStringFromExpression(argList.Arguments[0].Expression, attr, 0);
			var newName = GetStringFromExpression(argList.Arguments[1].Expression, attr, 1);
			if (deviceName is null || newName is null)
			{
				continue;
			}

			builder.Add(new(deviceName, newName));
		}

		return builder.ToImmutable();
	}

	private static ImmutableArray<AxisRename> GetAxisRenames(GeneratorAttributeSyntaxContext syntaxContext) =>
		GetAxisRenames(syntaxContext.TargetSymbol.GetAttributes());

	private static ImmutableArray<AxisRename> GetAxisRenames(IEnumerable<AttributeData> attributes)
	{
		var builder = ImmutableArray.CreateBuilder<AxisRename>();
		foreach (var attr in attributes)
		{
			if (attr.AttributeClass?.ToDisplayString() != RenameAxisAttributeMetadataName)
			{
				continue;
			}

			if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax { ArgumentList: { } argList }
			    || argList.Arguments.Count != 3)
			{
				continue;
			}

			var deviceName = GetStringFromExpression(argList.Arguments[0].Expression, attr, 0);
			if (deviceName is null)
			{
				continue;
			}

			var axisName = GetAxisFromExpression(argList.Arguments[1].Expression, attr, 1);
			if (axisName is null)
			{
				continue;
			}

			var newName = GetStringFromExpression(argList.Arguments[2].Expression, attr, 2);
			if (newName is null)
			{
				continue;
			}

			builder.Add(new(deviceName, axisName.Value, newName));
		}

		return builder.ToImmutable();
	}

	// Extracts a string value from an attribute argument expression.
	// Prefers the semantically-bound value; falls back to the rightmost identifier in a member
	// access for references to generated constants that don't exist in the model yet
	// (e.g. Devices.DeviceNames.LeftVpcStickWarBRD → "LeftVpcStickWarBRD").
	private static string? GetStringFromExpression(ExpressionSyntax expr, AttributeData attr, int index)
	{
		if (attr.ConstructorArguments.Length > index &&
		    attr.ConstructorArguments[index].Value is string str)
		{
			return str;
		}

		return expr switch
		{
			// String literal: "value"
			LiteralExpressionSyntax { Token.Value: string literalStr } => literalStr,
			// Generated const reference: Devices.DeviceNames.LeftVpcStickWarBRD → "LeftVpcStickWarBRD"
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
			_ => null,
		};
	}

	private static Axis? GetAxis(int value) => value switch
	{
		< 0 => null,
		_ => (Axis)value,
	};

	private static Axis? GetAxisFromExpression(ExpressionSyntax expr, AttributeData attr, int index)
	{
		if (attr.ConstructorArguments.Length > index)
		{
			var value = GetTypedConstantInt32(attr.ConstructorArguments[index], -1);
			if (value >= 0)
			{
				return GetAxis(value);
			}
		}

		// Fall back to syntax: Axis.Slider1 → parse enum member name
		if (expr is MemberAccessExpressionSyntax memberAccess
		    && Enum.TryParse<Axis>(memberAccess.Name.Identifier.ValueText, out var axis))
		{
			return axis;
		}

		return null;
	}

	private static bool HasLevel(GenerateDeviceInfosLevels levels, GenerateDeviceInfosLevels level) =>
		(levels & level) == level;

	private static void AppendSeparator(StringBuilder builder, ref bool wroteMember)
	{
		if (wroteMember)
		{
			builder.AppendLine();
		}

		wroteMember = true;
	}

	private static List<string> GetDirectInputDeviceConstantNames(
		ImmutableArray<DirectInputDeviceSnapshot> devices,
		ImmutableArray<VJoyDeviceSnapshot> outputDevices)
	{
		var baseNames = devices.Select(static device => ToIdentifier(device.ProductName)).ToArray();
		var counts = baseNames
			.GroupBy(static name => name, StringComparer.Ordinal)
			.ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

		// Base identifiers of output devices (e.g. "VJoyDevice" from "VJoyDevice1")
		// — DirectInput devices sharing this base are always numbered even when count == 1
		var outputBaseNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var od in outputDevices)
		{
			var id = $"VJoyDevice{od.DeviceId}";
			var end = id.Length - 1;
			while (end >= 0 && char.IsDigit(id[end])) end--;
			outputBaseNames.Add(id.Substring(0, end + 1));
		}

		var usedNames = new HashSet<string>(StringComparer.Ordinal);
		var groupCounters = new Dictionary<string, int>(StringComparer.Ordinal);
		var names = new List<string>(devices.Length);

		for (var index = 0; index < devices.Length; index++)
		{
			var name = baseNames[index];
			if (counts[name] > 1 || outputBaseNames.Contains(name))
			{
				var counter = groupCounters.GetValueOrDefault(name, 0);

				groupCounters[name] = ++counter;
				name += counter;
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

	private static GenerateDeviceInfosLevels GetDeviceInfoLevels(GeneratorAttributeSyntaxContext syntaxContext) =>
		GetDeviceInfoLevels(syntaxContext.Attributes);

	private static GenerateDeviceInfosLevels GetDeviceInfoLevels(IEnumerable<AttributeData> attributes)
	{
		var attribute = attributes.FirstOrDefault(static data =>
			data.AttributeClass?.ToDisplayString() == GenerateDeviceInfosAttributeMetadataName);

		if (attribute is null)
		{
			return GenerateDeviceInfosLevelsDefault;
		}

		if (attribute.ConstructorArguments.Length > 0)
		{
			return (GenerateDeviceInfosLevels)GetTypedConstantInt32(attribute.ConstructorArguments[0],
				(int)GenerateDeviceInfosLevelsDefault);
		}

		foreach (var namedArgument in attribute.NamedArguments)
		{
			if (namedArgument.Key == "Levels")
			{
				return (GenerateDeviceInfosLevels)GetTypedConstantInt32(namedArgument.Value,
					(int)GenerateDeviceInfosLevelsDefault);
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
		var byType = new Dictionary<INamedTypeSymbol, DeviceInfoTarget>(SymbolEqualityComparer.Default);

		foreach (var target in targets)
		{
			if (byType.TryGetValue(target.Type, out var existing))
			{
				byType[target.Type] = new(
					target.Type,
					existing.Levels | target.Levels,
					existing.DeviceRenames.IsDefaultOrEmpty ? target.DeviceRenames : existing.DeviceRenames,
					existing.AxisRenames.IsDefaultOrEmpty ? target.AxisRenames : existing.AxisRenames,
					existing.ButtonRenames.IsDefaultOrEmpty ? target.ButtonRenames : existing.ButtonRenames);
			}
			else
			{
				byType[target.Type] = target;
			}
		}

		return byType.Values;
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

	private readonly record struct DeviceInfoTarget(
		INamedTypeSymbol Type,
		GenerateDeviceInfosLevels Levels,
		ImmutableArray<DeviceRename> DeviceRenames,
		ImmutableArray<AxisRename> AxisRenames,
		ImmutableArray<ButtonRename> ButtonRenames)
	{
		public bool Equals(DeviceInfoTarget other) =>
			Levels == other.Levels
			&& SymbolEqualityComparer.Default.Equals(Type, other.Type)
			&& DeviceRenames.SequenceEqual(other.DeviceRenames)
			&& AxisRenames.SequenceEqual(other.AxisRenames)
			&& ButtonRenames.SequenceEqual(other.ButtonRenames);

		public override int GetHashCode()
		{
			var hc = new HashCode();
			hc.Add(Levels);
			hc.Add(Type, SymbolEqualityComparer.Default);
			hc.Add(DeviceRenames.Length);
			hc.Add(AxisRenames.Length);
			hc.Add(ButtonRenames.Length);
			return hc.ToHashCode();
		}
	}

	private readonly record struct AssemblyDeviceInfoTarget(
		GenerateDeviceInfosLevels Levels,
		ImmutableArray<DeviceRename> DeviceRenames,
		ImmutableArray<AxisRename> AxisRenames,
		ImmutableArray<ButtonRename> ButtonRenames)
	{
		public bool IsPresent => Levels != 0 || !DeviceRenames.IsDefaultOrEmpty;

		public bool Equals(AssemblyDeviceInfoTarget other) =>
			Levels == other.Levels
			&& DeviceRenames.SequenceEqual(other.DeviceRenames)
			&& AxisRenames.SequenceEqual(other.AxisRenames)
			&& ButtonRenames.SequenceEqual(other.ButtonRenames);

		public override int GetHashCode()
		{
			var hc = new HashCode();
			hc.Add(Levels);
			hc.Add(DeviceRenames.Length);
			hc.Add(AxisRenames.Length);
			hc.Add(ButtonRenames.Length);
			return hc.ToHashCode();
		}
	}

	private readonly record struct DeviceRename(string DeviceName, string NewName);

	private readonly record struct AxisRename(string DeviceName, Axis OriginalAxis, string NewPropertyName);

	private readonly record struct ButtonRename(string DeviceName, int Button, string NewPropertyName);
}