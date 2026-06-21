using System.Text;
using Darp.Luau.Generator.CreateFunction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Darp.Luau.Generator;

/// <summary> Interceptor generator for LuauState.CreateFunction. </summary>
[Generator]
public sealed class CreateFunctionGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<IInvocationOperation> matchingInvocations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: CreateFunctionDiscovery.IsCandidate,
                transform: CreateFunctionDiscovery.GetMatchingInvocation
            )
            .Where(static op => op is not null)
            .Select(static (op, _) => op!);

        context.RegisterSourceOutput(
            matchingInvocations,
            static (spc, invocation) =>
            {
                CreateFunctionAnalysisResult result = CreateFunctionSignatureAnalyzer.Analyze(invocation);
                foreach (Diagnostic diagnostic in result.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);

                if (result.Model is not { } model)
                    return;

                bool emitsSource = CreateFunctionEmitter.TryEmit(model, out string? source);
                if (!emitsSource)
                    return;

                var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
                spc.AddSource(CreateFunctionEmitter.GetHintName(model), sourceText);
            }
        );
    }
}
