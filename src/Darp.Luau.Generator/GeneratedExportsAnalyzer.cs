using System.Collections.Immutable;
using Darp.Luau.Generator.GeneratedExports;
using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Darp.Luau.Generator;

/// <summary> Reports diagnostics for generated Luau libraries and userdata. </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedExportsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            DiagnosticDescriptors.LuauLibraryTypeMustBePartialDescriptor,
            DiagnosticDescriptors.LuauUserdataTypeMustBePartialDescriptor,
            DiagnosticDescriptors.DuplicateLuauMemberNameDescriptor,
            DiagnosticDescriptors.UnsupportedGeneratedPropertyTypeDescriptor,
            DiagnosticDescriptors.InstanceLibraryStructNotSupportedDescriptor,
            DiagnosticDescriptors.UnsupportedGeneratedFunctionShapeDescriptor,
            DiagnosticDescriptors.LuauExportPathConflictDescriptor,
            DiagnosticDescriptors.InvalidLuauExportPathDescriptor,
            DiagnosticDescriptors.LibraryPropertyMustBeReadOnlyDescriptor,
            DiagnosticDescriptors.GeneratedUserdataManualInteropConflictDescriptor,
            DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
            DiagnosticDescriptors.LuauExportPathSegmentRequiresBracketAccessDescriptor,
        ];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var apiSymbols = LuauApiSymbols.Create(compilationContext.Compilation);
            if (apiSymbols is null)
                return;

            compilationContext.RegisterSymbolAction(
                context => AnalyzeNamedType(context, apiSymbols),
                SymbolKind.NamedType
            );
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, LuauApiSymbols apiSymbols)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        AttributeData? libraryAttribute = apiSymbols.GetLibraryAttribute(type);
        AttributeData? userdataAttribute = apiSymbols.GetUserdataAttribute(type);
        if (libraryAttribute is null && userdataAttribute is null)
            return;

        LuauExportedTypeKind expectedKind = libraryAttribute is not null
            ? LuauExportedTypeKind.Library
            : LuauExportedTypeKind.Userdata;
        GeneratedExportsTypeAnalysis analysis = ExportAnalyzer.AnalyzeType(type, expectedKind, context.Compilation);
        foreach (Diagnostic diagnostic in analysis.Diagnostics)
            context.ReportDiagnostic(diagnostic);
    }
}
