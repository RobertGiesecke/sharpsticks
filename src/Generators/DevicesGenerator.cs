using System.Collections.Immutable;
using System.Text;
using Collections.Pooled;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SharpSticks.InputAbstractions;

namespace SharpSticks.Generators;

[Generator]
public sealed class DevicesGenerator : IIncrementalGenerator
{
	private const string NamespaceRoot = $"{nameof(SharpSticks)}.";
	private const string DefaultDevicesClassName = "Devices";

	private const string GenerateDeviceInfosAttributeMetadataName = nameof(GenerateDeviceInfosAttribute);
	private const string RenameDeviceAttributeMetadataName = nameof(RenameDeviceAttribute);
	private const string RenameAxisAttributeMetadataName = nameof(RenameAxis);
	private const string RenameButtonAttributeMetadataName = nameof(RenameButton);
	private const GenerateDeviceInfosLevels GenerateDeviceInfosLevelsDefault = GenerateDeviceInfosLevels.DeviceNames;

	private static readonly DiagnosticDescriptor TypeMustBePartial = new(
		"SACIG001",
		"GenerateDeviceInfos target must be partial",
		"Type '{0}' must be partial to receive generated device info members",
		$"{NamespaceRoot}Generators",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor InputDevicesUnavailable = new(
		"SACIG002",
		"Input device snapshot unavailable",
		"Input device info generation could not enumerate devices: {0}",
		$"{NamespaceRoot}Generators",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor OutputDevicesUnavailable = new(
		"SACIG003",
		"Output device snapshot unavailable",
		"Output device info generation could not enumerate output devices: {0}",
		$"{NamespaceRoot}Generators",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		GeneratorLog.Log("DevicesGenerator.Initialize");

		var deviceInfoTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
				GenerateDeviceInfosAttributeMetadataName,
				static (node, _) => node is TypeDeclarationSyntax or CompilationUnitSyntax,
				static (syntaxContext, _) =>
				{
					GeneratorLog.Log(
						$"SyntaxProvider: select {nameof(DeviceInfoTarget)} for {syntaxContext.TargetSymbol.GetType().FullName} {syntaxContext.TargetSymbol.ToDisplayString()}");

					var deviceInfoTarget = syntaxContext.TargetSymbol switch
					{
						INamedTypeSymbol targetSymbol => new DeviceInfoTarget(
							DeviceType.FromNameTypeSymbol(targetSymbol),
							syntaxContext.TargetNode.GetLocation(),
							GetDeviceInfoLevels(syntaxContext.Attributes),
							GetDeviceRenames(targetSymbol.GetAttributes()),
							GetAxisRenames(targetSymbol.GetAttributes()),
							GetButtonRenames(targetSymbol.GetAttributes())),
						ISourceAssemblySymbol targetSymbol
							when targetSymbol.Compilation.GetTypeByMetadataName(
									     DefaultDevicesClassName) is
								     { } d
							     && targetSymbol.GetAttributes().AddRange(d.GetAttributes()) is var allAttributes
							=> new DeviceInfoTarget(
								DeviceType.FromNameTypeSymbol(d),
								syntaxContext.TargetNode.GetLocation(),
								GetDeviceInfoLevels(allAttributes),
								GetDeviceRenames(allAttributes),
								GetAxisRenames(allAttributes),
								GetButtonRenames(allAttributes)),
						ISourceAssemblySymbol targetSymbol
							when targetSymbol.GetAttributes() is var allAttributes
							=> new DeviceInfoTarget(
								new()
								{
									Namespace = null,
									FullyQualifiedDisplayString = DefaultDevicesClassName,
									DisplayString = DefaultDevicesClassName,
									ContainingTypes = [],
									Name = DefaultDevicesClassName,
									IsStatic = true,
									IsNested = false,
									IsPartial = true,
									Accessibility = Accessibility.Internal,
									TypeKind = DeviceTypeKind.Class,
									ClrTypeKind = TypeKind.Unknown,
									TypeDeclaration = DefaultDevicesClassName
								},
								syntaxContext.TargetNode.GetLocation(),
								GetDeviceInfoLevels(allAttributes),
								GetDeviceRenames(allAttributes),
								GetAxisRenames(allAttributes),
								GetButtonRenames(allAttributes)),
						_ => throw new ArgumentOutOfRangeException(nameof(syntaxContext.TargetSymbol),
							syntaxContext.TargetSymbol,
							$"unsupported target symbol: {syntaxContext.TargetSymbol.ToDisplayString()}"),
					};

					GeneratorLog.Log(
						$"SyntaxProvider: use {nameof(DeviceInfoTarget)} {new { deviceInfoTarget.FirstLocation, deviceInfoTarget.DeviceType.FullyQualifiedDisplayString }}");
					return deviceInfoTarget;
				})
			.Where(static target => target.DeviceType.ClrTypeKind is TypeKind.Class or TypeKind.Struct);

		context.RegisterSourceOutput(
			deviceInfoTargets.Collect(),
			static (sourceProductionContext, targets) =>
			{
				GeneratorLog.Log($"RegisterSourceOutput: {nameof(GenerateDeviceInfos)} targets={targets.Length}");
				GenerateDeviceInfos(sourceProductionContext, targets);
			});
	}

	private static void GenerateDeviceInfos(SourceProductionContext context, ImmutableArray<DeviceInfoTarget> targets)
	{
		if (targets.IsDefaultOrEmpty)
		{
			GeneratorLog.Log(
				$"{nameof(GenerateDeviceInfos)}: empty targets");

			return;
		}

		if (!DeviceSnapshots.TryEnumerateInputDevices(out var directInputDevices, out var directInputError))
		{
			context.ReportDiagnostic(Diagnostic.Create(InputDevicesUnavailable, Location.None, directInputError));
			directInputDevices = ImmutableArray<InputDeviceSnapshot>.Empty;
		}

		if (!DeviceSnapshots.TryEnumerateOutputDevices(out var outputDevices, out var outputError))
		{
			context.ReportDiagnostic(Diagnostic.Create(OutputDevicesUnavailable, Location.None, outputError));
			outputDevices = ImmutableArray<OutputDeviceSnapshot>.Empty;
		}

		foreach (var target in CoalesceTargets(targets))
		{
			if (!target.DeviceType.IsPartial)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					TypeMustBePartial,
					target.FirstLocation,
					target.DeviceType.DisplayString));
				continue;
			}

			if (target.Levels == 0)
			{
				continue;
			}

			var source = GenerateDeviceInfosSource(
				target.DeviceType, target.Levels,
				target.DeviceRenames, target.AxisRenames, target.ButtonRenames,
				directInputDevices, outputDevices);
			var hintName = GetDeviceInfosHintName(target.DeviceType);
			GeneratorLog.Log(
				$"AddSource: {hintName} ({source.Length} chars, target={target.DeviceType.DisplayString})");
			context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));

			var fqn = target.DeviceType.FullyQualifiedDisplayString;
			var globalUsings = BuildGlobalUsings(fqn, target.Levels);
			var globalUsingsHintName = hintName.Replace(".DeviceInfos.g.cs", ".GlobalUsings.g.cs");
			GeneratorLog.Log($"AddSource: {globalUsingsHintName} ({globalUsings.Length} chars)");
			context.AddSource(globalUsingsHintName, SourceText.From(globalUsings, Encoding.UTF8));
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

	private static string GenerateDeviceInfosSource(
		DeviceType target,
		GenerateDeviceInfosLevels levels,
		ImmutableArray<DeviceRename> deviceRenames,
		ImmutableArray<AxisRename> axisRenames,
		ImmutableArray<ButtonRename> buttonRenames,
		ImmutableArray<InputDeviceSnapshot> directInputDevices,
		ImmutableArray<OutputDeviceSnapshot> outputDevices)
	{
		var builder = new StringBuilder();
		var namespaceName = target.Namespace;
		var containingTypes = target.ContainingTypes;

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
				.Append(GetAccessibility(containingType.Accessibility))
				.Append(' ');

			if (containingType.IsStatic)
			{
				builder.Append("static ");
			}

			builder.Append("partial ")
				.Append(GetTypeKindCode(containingType.TypeKind))
				.Append(' ')
				.Append(containingType.TypeDeclaration)
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
		ImmutableArray<InputDeviceSnapshot> directInputDevices,
		ImmutableArray<OutputDeviceSnapshot> outputDevices,
		int indentLevel)
	{
		var directInputNames = GetDirectInputDeviceConstantNames(directInputDevices, outputDevices);
		var deviceIdentifiers = GetDeviceIdentifiers(directInputDevices, directInputNames, deviceRenames);
		using var originalNameSet = new PooledSet<string>(directInputNames, StringComparer.Ordinal);

		// Inputs whose ProductGuid matches an output's expected input-side Guid are
		// "output counterparts" (e.g. vJoy's DirectInput entries). Their names still
		// appear in DeviceNames so RenameDevice attributes can target them, but they
		// are excluded from DeviceIds — driving them as inputs makes no sense — and
		// from TypedDevices, where they're rendered as typed OUTPUT classes instead.
		using var outputProductGuids = new PooledSet<Guid>(
			outputDevices.Select(static d => d.InputProductGuid));
		bool IsOutputCounterpart(int index) => outputProductGuids.Contains(directInputDevices[index].ProductGuid);
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
				if (IsOutputCounterpart(index))
				{
					continue;
				}

				builder.Append(memberIndent)
					.Append("public const int ")
					.Append(directInputNames[index])
					.Append(" = ")
					.Append(directInputDevices[index].DeviceId)
					.AppendLine(";");
			}

			for (var index = 0; index < directInputDevices.Length; index++)
			{
				if (IsOutputCounterpart(index))
				{
					continue;
				}

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

		if (!HasLevel(levels, GenerateDeviceInfosLevels.TypedDevices))
		{
			return;
		}

		{
			for (var index = 0; index < directInputDevices.Length; index++)
			{
				if (IsOutputCounterpart(index))
				{
					continue;
				}

				// Pass the renamed identifier (deviceIdentifiers[index]) as extraIdentifier so
				// [RenameAxis(DeviceNames.<RenamedAlias>, ...)] and the corresponding
				// RenameButton both resolve against the alias the user actually wrote — same
				// behaviour the output typed-class branch already has via vjoyIdentifier.
				using var axisPropertyNames = BuildAxisPropertyNames(directInputDevices[index].ProductName,
					directInputNames[index], axisRenames, deviceIdentifiers[index]);
				using var buttonPropertyNames = BuildButtonPropertyNames(directInputDevices[index].ProductName,
					directInputNames[index], buttonRenames, deviceIdentifiers[index]);
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
				using var outputAxisNames =
					BuildAxisPropertyNames(deviceName, vjoyBaseName, axisRenames, vjoyIdentifier);
				using var outputButtonNames =
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
				if (IsOutputCounterpart(index))
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
		InputDeviceSnapshot device,
		PooledDictionary<Axis, string> axisPropertyNames,
		PooledDictionary<int, string> buttonPropertyNames,
		string indent,
		string memberIndent)
	{
		var classIndent = indent;
		var innerIndent = memberIndent;
		var innerMemberIndent = memberIndent + "\t";

		builder.Append(classIndent).Append("public sealed record Typed").Append(deviceIdentifier)
			.Append(": IJoystickDevice").AppendLine();
		builder.Append(classIndent).AppendLine("{");

		builder.Append(innerIndent)
			.Append("public const string DeviceName = DeviceNames.")
			.Append(deviceOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent)
			.Append("public const int DeviceId = DeviceIds.")
			.Append(deviceOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent)
			.AppendLine("int IJoystickDevice.DeviceId => DeviceId;");

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
		PooledDictionary<Axis, string> axisPropertyNames,
		uint buttonCount,
		PooledDictionary<int, string> buttonPropertyNames,
		string indent,
		string memberIndent)
	{
		var innerIndent = memberIndent;
		var innerMemberIndent = memberIndent + "\t";

		builder.Append(indent).Append("public sealed record Typed").Append(vjoyIdentifier).Append(": IOutputDevice")
			.AppendLine();
		builder.Append(indent).AppendLine("{");

		builder.Append(innerIndent).Append("public const string DeviceName = DeviceNames.").Append(vjoyOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent).Append("public const uint DeviceId = OutputDeviceIds.").Append(vjoyOriginalName)
			.AppendLine(";");
		builder.Append(innerIndent)
			.AppendLine("uint IOutputDevice.DeviceId => DeviceId;");

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
		ImmutableArray<InputDeviceSnapshot> devices,
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

	private static PooledDictionary<Axis, string> BuildAxisPropertyNames(
		string deviceProductName,
		string deviceBaseIdentifier,
		ImmutableArray<AxisRename> axisRenames,
		string? extraIdentifier = null)
	{
		var result = new PooledDictionary<Axis, string>(axisRenames.Length);
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

	private static string AxisPropertyName(Axis axisName, PooledDictionary<Axis, string> axisPropertyNames) =>
		axisPropertyNames.GetValueOrDefault(axisName, axisName.ToString());

	private static PooledDictionary<int, string> BuildButtonPropertyNames(
		string deviceProductName,
		string deviceBaseIdentifier,
		ImmutableArray<ButtonRename> buttonRenames,
		string? extraIdentifier = null)
	{
		var result = new PooledDictionary<int, string>(buttonRenames.Length);
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
		// Syntax-form first. A const reference like `DeviceNames.VJoyDevice1` and a literal
		// `"vJoy Device"` would both have the same compile-time string Value ("vJoy Device"),
		// so two distinct const symbols (VJoyDevice1, VJoyDevice2) referring to devices with
		// duplicate product names would be indistinguishable if we used the value. The symbol
		// identifier ("VJoyDevice1") IS the disambiguator — that's why the generator emits
		// numbered constants in the first place.
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: string literalStr }:
				return literalStr;
			case MemberAccessExpressionSyntax memberAccess:
				return memberAccess.Name.Identifier.ValueText;
			case IdentifierNameSyntax identifier:
				return identifier.Identifier.ValueText;
		}

		// Fallback: the argument is a more complex expression (e.g. ternary, method call)
		// that the C# compiler still resolved to a constant string. Use the value, accepting
		// that we lose disambiguation in that case.
		if (attr.ConstructorArguments.Length > index &&
		    attr.ConstructorArguments[index].Value is string str)
		{
			return str;
		}

		return null;
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
		ImmutableArray<InputDeviceSnapshot> devices,
		ImmutableArray<OutputDeviceSnapshot> outputDevices)
	{
		var baseNames = devices.Select(static device => ToIdentifier(device.ProductName)).ToArray();
		var counts = baseNames
			.GroupBy(static name => name, StringComparer.Ordinal)
			.ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

		// Base identifiers of output devices (e.g. "VJoyDevice" from "VJoyDevice1")
		// — DirectInput devices sharing this base are always numbered even when count == 1
		using var outputBaseNames = new PooledSet<string>(StringComparer.Ordinal);
		foreach (var od in outputDevices)
		{
			var id = $"VJoyDevice{od.DeviceId}";
			var end = id.Length - 1;
			while (end >= 0 && char.IsDigit(id[end])) end--;
			outputBaseNames.Add(id.Substring(0, end + 1));
		}

		using var usedNames = new PooledSet<string>(StringComparer.Ordinal);
		using var groupCounters = new PooledDictionary<string, int>(StringComparer.Ordinal);
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

	private static ImmutableArray<DeviceInfoTarget> CoalesceTargets(ImmutableArray<DeviceInfoTarget> targets)
	{
		using var byType = new PooledDictionary<string, DeviceInfoTarget>();

		foreach (var target in targets)
		{
			var key = target.DeviceType.FullyQualifiedDisplayString;
			if (byType.TryGetValue(key, out var existing))
			{
				byType[key] = new(
					target.DeviceType,
					null,
					existing.Levels | target.Levels,
					existing.DeviceRenames.IsDefaultOrEmpty ? target.DeviceRenames : existing.DeviceRenames,
					existing.AxisRenames.IsDefaultOrEmpty ? target.AxisRenames : existing.AxisRenames,
					existing.ButtonRenames.IsDefaultOrEmpty ? target.ButtonRenames : existing.ButtonRenames);
			}
			else
			{
				byType[key] = target;
			}
		}

		var result = ImmutableArray.CreateBuilder<DeviceInfoTarget>(byType.Count);
		foreach (var value in byType.Values)
		{
			result.Add(value);
		}

		return result.MoveToImmutable();
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

	private static ImmutableArray<DeviceContainingType> GetContainingTypeChain(INamedTypeSymbol target)
	{
		var builder = ImmutableArray.CreateBuilder<DeviceContainingType>();
		for (var current = target; current is not null; current = current.ContainingType)
		{
			builder.Add(new()
			{
				IsStatic = current.IsStatic,
				IsNested = true,
				IsPartial = IsPartial(current),
				Accessibility = current.DeclaredAccessibility,
				Name = current.Name,
				TypeKind = GetDeviceTypeKind(current),
				ClrTypeKind = current.TypeKind,
				TypeDeclaration = GetTypeDeclaration(current),
			});
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

	private static string GetDeviceInfosHintName(DeviceType target)
	{
		var displayName = target.FullyQualifiedDisplayString;
		var builder = new StringBuilder(displayName.Length);

		foreach (var character in displayName)
		{
			builder.Append(char.IsLetterOrDigit(character) ? character : '_');
		}

		return builder + ".DeviceInfos.g.cs";
	}

	private static string GetAccessibility(Accessibility declaredAccessibility) =>
		declaredAccessibility switch
		{
			Accessibility.Public => "public",
			Accessibility.Protected => "protected",
			Accessibility.Internal => "internal",
			Accessibility.ProtectedOrInternal => "protected internal",
			Accessibility.ProtectedAndInternal => "private protected",
			Accessibility.Private => "private",
			_ => "internal",
		};

	private static DeviceTypeKind GetDeviceTypeKind(INamedTypeSymbol symbol) =>
		symbol switch
		{
			{ IsRecord: true, TypeKind: TypeKind.Struct } => DeviceTypeKind.RecordStruct,
			{ IsRecord: true } => DeviceTypeKind.Record,
			{ TypeKind: TypeKind.Struct } => DeviceTypeKind.Struct,
			_ => DeviceTypeKind.Class,
		};

	private static string GetTypeKindCode(DeviceTypeKind symbol) =>
		symbol switch
		{
			DeviceTypeKind.Class => "class",
			DeviceTypeKind.Record => "record",
			DeviceTypeKind.RecordStruct => "record struct",
			DeviceTypeKind.Struct => "struct",
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null),
		};

	private static string GetTypeDeclaration(INamedTypeSymbol symbol)
	{
		if (symbol.TypeParameters.Length == 0)
		{
			return symbol.Name;
		}

		return symbol.Name + "<" + string.Join(", ", symbol.TypeParameters.Select(static parameter => parameter.Name)) +
		       ">";
	}

	private enum DeviceTypeKind
	{
		Class,
		Struct,
		Record,
		RecordStruct,
	}

	abstract record BaseDeviceType
	{
		public required string Name { get; init; }
		public required bool IsStatic { get; init; }

		// ReSharper disable once MemberHidesStaticFromOuterClass
		public required bool IsNested { get; init; }

		// ReSharper disable once MemberHidesStaticFromOuterClass
		public required bool IsPartial { get; init; }
		public required Accessibility Accessibility { get; init; }
		public required DeviceTypeKind TypeKind { get; init; }
		public required TypeKind ClrTypeKind { get; init; }
		public required string TypeDeclaration { get; init; }

		public override int GetHashCode()
		{
			var hc = new HashCode();
			AddToHashCode(ref hc);
			return hc.ToHashCode();
		}

		protected virtual void AddToHashCode(ref HashCode hashCode)
		{
			hashCode.Add(Name);
			hashCode.Add(IsNested);
			hashCode.Add(IsStatic);
			hashCode.Add(IsPartial);
			hashCode.Add(Accessibility);
			hashCode.Add(TypeKind);
			hashCode.Add(ClrTypeKind);
			hashCode.Add(TypeDeclaration);
		}
	}

	sealed record DeviceContainingType : BaseDeviceType;

	sealed record DeviceType : BaseDeviceType
	{
		public static DeviceType FromNameTypeSymbol(INamedTypeSymbol typeSymbol)
		{
			return new()
			{
				Name = typeSymbol.Name,
				IsNested = typeSymbol.ContainingType is not null,
				IsStatic = typeSymbol.IsStatic,
				IsPartial = IsPartial(typeSymbol),
				FullyQualifiedDisplayString = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				DisplayString = typeSymbol.ToDisplayString(),
				Accessibility = typeSymbol.DeclaredAccessibility,
				Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
					? null
					: typeSymbol.ContainingNamespace.ToDisplayString(),
				TypeKind = GetDeviceTypeKind(typeSymbol),
				ClrTypeKind = typeSymbol.TypeKind,
				TypeDeclaration = GetTypeDeclaration(typeSymbol),
				ContainingTypes = GetContainingTypeChain(typeSymbol),
			};
		}

		public required string? Namespace { get; init; }
		public required string FullyQualifiedDisplayString { get; init; }
		public required string DisplayString { get; init; }

		// ReSharper disable once TypeWithSuspiciousEqualityIsUsedInRecord.Local
		public required ImmutableArray<DeviceContainingType> ContainingTypes { get; init; }

#pragma warning disable CS8851 // Record defines 'Equals' but not 'GetHashCode'.
		public bool Equals(DeviceType? other)
#pragma warning restore CS8851 // Record defines 'Equals' but not 'GetHashCode'.
		{
			if (other is null)
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			return base.Equals(other) &&
			       Namespace == other.Namespace &&
			       ContainingTypes.SequenceEqual(other.ContainingTypes);
		}

		protected override void AddToHashCode(ref HashCode hashCode)
		{
			base.AddToHashCode(ref hashCode);
			hashCode.Add(Namespace);
			hashCode.Add(ContainingTypes.Length);
		}
	}

	private readonly record struct DeviceInfoTarget
	{
		public DeviceInfoTarget(DeviceType DeviceType,
			Location? firstLocation,
			GenerateDeviceInfosLevels Levels,
			ImmutableArray<DeviceRename> DeviceRenames,
			ImmutableArray<AxisRename> AxisRenames,
			ImmutableArray<ButtonRename> ButtonRenames)
		{
			this.DeviceType = DeviceType;
			FirstLocation = firstLocation;
			this.Levels = Levels;
			this.DeviceRenames = DeviceRenames;
			this.AxisRenames = AxisRenames;
			this.ButtonRenames = ButtonRenames;
		}


		public bool Equals(DeviceInfoTarget other) =>
			Levels == other.Levels
			&& DeviceType.Equals(other.DeviceType)
			&& GetLocationTuple(FirstLocation).Equals(GetLocationTuple(other.FirstLocation))
			&& DeviceRenames.SequenceEqual(other.DeviceRenames)
			&& AxisRenames.SequenceEqual(other.AxisRenames)
			&& ButtonRenames.SequenceEqual(other.ButtonRenames);

		private static (int? Start, bool? IsInSource, string? FilePath) GetLocationTuple(Location? location) => (
			location?.SourceSpan.Start,
			location?.IsInSource,
			location?.SourceTree?.FilePath);

		public override int GetHashCode()
		{
			var hc = new HashCode();
			hc.Add(Levels);
			hc.Add(DeviceType);
			hc.Add(DeviceRenames.Length);
			hc.Add(AxisRenames.Length);
			hc.Add(ButtonRenames.Length);
			return hc.ToHashCode();
		}

		public DeviceType DeviceType { get; init; }
		public Location? FirstLocation { get; init; }
		public GenerateDeviceInfosLevels Levels { get; init; }
		public ImmutableArray<DeviceRename> DeviceRenames { get; init; }
		public ImmutableArray<AxisRename> AxisRenames { get; init; }
		public ImmutableArray<ButtonRename> ButtonRenames { get; init; }
	}


	private readonly record struct DeviceRename(string DeviceName, string NewName);

	private readonly record struct AxisRename(string DeviceName, Axis OriginalAxis, string NewPropertyName);

	private readonly record struct ButtonRename(string DeviceName, int Button, string NewPropertyName);
}