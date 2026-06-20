using System.Collections.Immutable;
using System.Text;
using Darp.Luau.Generator.GeneratedExports;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Darp.Luau.Generator;

/// <summary>
/// Reports diagnostics and emits runtime registration code for generator-owned Luau libraries.
/// </summary>
[Generator]
public sealed class GeneratedExportsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol> candidateTypes = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, token) =>
                    ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, token) as INamedTypeSymbol
            )
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!);

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol> Types)> input =
            context.CompilationProvider.Combine(candidateTypes.Collect());

        context.RegisterSourceOutput(
            input,
            static (spc, data) =>
            {
                var builder = GeneratedExportsModelBuilder.Create(data.Compilation);
                if (builder is null)
                    return;

                var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (
                    INamedTypeSymbol type in data.Types.OrderBy(
                        static x => x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        StringComparer.Ordinal
                    )
                )
                {
                    if (!visited.Add(type))
                        continue;

                    GeneratedExportsTypeAnalysis analysis = builder.AnalyzeType(type);
                    foreach (Diagnostic diagnostic in analysis.Diagnostics)
                        spc.ReportDiagnostic(diagnostic);

                    if (
                        analysis.Model is not { Kind: LuauExportedTypeKind.Library } model
                        || !analysis.CanEmitRegisterMethod
                    )
                        continue;

                    if (!GeneratedLibraryExportsEmitter.TryEmit(type, model, out string? source, out List<Diagnostic> emitDiagnostics))
                    {
                        foreach (Diagnostic diagnostic in emitDiagnostics)
                            spc.ReportDiagnostic(diagnostic);
                        continue;
                    }

                    foreach (Diagnostic diagnostic in emitDiagnostics)
                        spc.ReportDiagnostic(diagnostic);

                    var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
                    spc.AddSource(GetHintName(type), sourceText);
                }
            }
        );
    }

    private static string GetHintName(INamedTypeSymbol type)
    {
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new StringBuilder(name.Length + 32);
        foreach (char c in name)
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        builder.Append(".LuauLibrary.g.cs");
        return builder.ToString();
    }
}
