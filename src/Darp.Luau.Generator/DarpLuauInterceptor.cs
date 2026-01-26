using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Darp.Luau.Generator;

/// <summary> Interceptor generator for LuauState.CreateMethod </summary>
[Generator]
public sealed class DarpLuauInterceptor : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1) Find the *specific* method symbol: LuauState.CreateFunction(Delegate)
        IncrementalValueProvider<IMethodSymbol?> targetMethodProvider = context.CompilationProvider.Select(
            static (comp, _) =>
            {
                INamedTypeSymbol? luauState = comp.GetTypeByMetadataName("Darp.Luau.LuauState");
                INamedTypeSymbol? delegateType = comp.GetTypeByMetadataName("System.Delegate");
                if (luauState is null || delegateType is null)
                    return null;
                return luauState
                    .GetMembers("CreateFunction")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m.Parameters.Length == 1
                        && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, delegateType)
                    );
            }
        );

        // 2) Find candidate invocation syntax nodes (cheap syntax filter first)
        IncrementalValuesProvider<IInvocationOperation> invocationOps = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node
                        is InvocationExpressionSyntax
                        {
                            Expression:
                                //asd
                                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CreateFunction" }
                                //asd
                                or IdentifierNameSyntax { Identifier.ValueText: "CreateFunction" }
                        },
                transform: static (ctx, token) =>
                {
                    // 3) Resolve to operation (semantic)
                    var inv = (InvocationExpressionSyntax)ctx.Node;
                    return ctx.SemanticModel.GetOperation(inv, token) as IInvocationOperation;
                }
            )
            .Where(static op => op is not null)
            .Select(static (op, _) => op!);

        // 4) Join and filter by the exact symbol
        var matchingInvocations = invocationOps
            .Combine(targetMethodProvider)
            .Where(static pair =>
            {
                (IInvocationOperation op, IMethodSymbol? target) = pair;
                if (target is null)
                    return false;
                // Exact match (recommended)
                return SymbolEqualityComparer.Default.Equals(op.TargetMethod.OriginalDefinition, target);
            })
            .Select(static (pair, _) => pair.Left);

        // If you truly need “all of them at once”, collect:
        var all = matchingInvocations.Collect(); // ImmutableArray<IInvocationOperation>

        context.RegisterSourceOutput(
            all,
            static (spc, calls) =>
            {
                if (calls.IsEmpty)
                    return;
                bool emitsSource = CreateFunctionInterceptorsEmitter.TryEmit(
                    calls,
                    out string? source,
                    out List<Diagnostic> diagnostics
                );
                foreach (Diagnostic diagnostic in diagnostics)
                    spc.ReportDiagnostic(diagnostic);
                if (!emitsSource)
                    return;
                var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
                spc.AddSource("Darp.Luau.LuauState.CreateFunctionInterceptors.g.cs", sourceText);
            }
        );
    }
}
