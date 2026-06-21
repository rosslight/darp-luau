using System.Text;
using Darp.Luau.Generator.GeneratedExports;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Darp.Luau.Generator;

/// <summary>
/// Reports diagnostics and emits runtime registration code for generator-owned Luau libraries and userdata.
/// </summary>
[Generator]
public sealed class GeneratedExportsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<GeneratedExportsTypeAnalysis> libraryAnalyses =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Darp.Luau.LuauLibraryAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => AnalyzeType(ctx, LuauExportedTypeKind.Library)
            );

        IncrementalValuesProvider<GeneratedExportsTypeAnalysis> userdataAnalyses =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Darp.Luau.LuauUserdataAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => AnalyzeType(ctx, LuauExportedTypeKind.Userdata)
            );

        context.RegisterSourceOutput(libraryAnalyses, Emit);
        context.RegisterSourceOutput(userdataAnalyses, Emit);
    }

    private static GeneratedExportsTypeAnalysis AnalyzeType(
        GeneratorAttributeSyntaxContext context,
        LuauExportedTypeKind expectedKind
    )
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return new GeneratedExportsTypeAnalysis(null, [], CanEmitSource: false);

        return ExportAnalyzer.AnalyzeType(type, expectedKind, context.SemanticModel.Compilation);
    }

    private static void Emit(SourceProductionContext spc, GeneratedExportsTypeAnalysis analysis)
    {
        foreach (Diagnostic diagnostic in analysis.Diagnostics)
            spc.ReportDiagnostic(diagnostic);

        if (analysis.Model is not { } model || !analysis.CanEmitSource)
            return;

        string? source;
        List<Diagnostic> emitDiagnostics;
        bool emitted = model.Kind switch
        {
            LuauExportedTypeKind.Library => LibraryEmitter.TryEmit(model, out source, out emitDiagnostics),
            LuauExportedTypeKind.Userdata => UserdataEmitter.TryEmit(model, out source, out emitDiagnostics),
            _ => throw new InvalidOperationException($"Unsupported export kind '{model.Kind}'."),
        };

        foreach (Diagnostic diagnostic in emitDiagnostics)
            spc.ReportDiagnostic(diagnostic);

        if (!emitted)
            return;

        var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
        spc.AddSource(model.HintName, sourceText);
    }
}
