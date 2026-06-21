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
        IncrementalValuesProvider<GeneratedExportSurfaceIr> libraryModels =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Darp.Luau.LuauLibraryAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => AnalyzeType(ctx, LuauExportedTypeKind.Library)
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName("GeneratedExportsLibraryModels");

        IncrementalValuesProvider<GeneratedExportSurfaceIr> userdataModels =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Darp.Luau.LuauUserdataAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => AnalyzeType(ctx, LuauExportedTypeKind.Userdata)
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName("GeneratedExportsUserdataModels");

        context.RegisterSourceOutput(libraryModels, Emit);
        context.RegisterSourceOutput(userdataModels, Emit);
    }

    private static GeneratedExportSurfaceIr? AnalyzeType(
        GeneratorAttributeSyntaxContext context,
        LuauExportedTypeKind expectedKind
    )
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return null;

        GeneratedExportsTypeAnalysis analysis = ExportAnalyzer.AnalyzeType(
            type,
            expectedKind,
            context.SemanticModel.Compilation
        );
        return analysis.CanEmitSource ? analysis.Model : null;
    }

    private static void Emit(SourceProductionContext spc, GeneratedExportSurfaceIr model)
    {
        string? source;
        bool emitted = model.Kind switch
        {
            LuauExportedTypeKind.Library => LibraryEmitter.TryEmit(model, out source),
            LuauExportedTypeKind.Userdata => UserdataEmitter.TryEmit(model, out source),
            _ => throw new InvalidOperationException($"Unsupported export kind '{model.Kind}'."),
        };

        if (!emitted)
            return;

        var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
        spc.AddSource(model.HintName, sourceText);
    }
}
