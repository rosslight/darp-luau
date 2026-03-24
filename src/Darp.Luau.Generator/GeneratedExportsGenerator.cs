using System.Collections.Immutable;
using Darp.Luau.Generator.GeneratedExports;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Darp.Luau.Generator;

/// <summary>
/// Reports phase-1 diagnostics for generator-owned Luau exports.
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
                foreach (INamedTypeSymbol type in data.Types)
                {
                    if (!visited.Add(type))
                        continue;

                    GeneratedExportsTypeAnalysis analysis = builder.AnalyzeType(type);
                    foreach (Diagnostic diagnostic in analysis.Diagnostics)
                        spc.ReportDiagnostic(diagnostic);
                }
            }
        );
    }
}
