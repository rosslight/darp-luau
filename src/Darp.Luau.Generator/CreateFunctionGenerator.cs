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

        IncrementalValuesProvider<CreateFunctionAnalysisResult> analyses = matchingInvocations.Select(
            static (invocation, _) => CreateFunctionSignatureAnalyzer.Analyze(invocation)
        );

        IncrementalValuesProvider<CreateFunctionModel> models = analyses
            .Select(static (analysis, _) => analysis.Model)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(analyses, ReportDiagnostics);
        context.RegisterSourceOutput(models, Emit);
    }

    private static void ReportDiagnostics(SourceProductionContext spc, CreateFunctionAnalysisResult analysis)
    {
        foreach (Diagnostic diagnostic in analysis.Diagnostics)
            spc.ReportDiagnostic(diagnostic);
    }

    private static void Emit(SourceProductionContext spc, CreateFunctionModel model)
    {
        bool emitsSource = CreateFunctionEmitter.TryEmit(model, out string? source);
        if (!emitsSource)
            return;

        var sourceText = SourceText.From(source ?? string.Empty, Encoding.UTF8, SourceHashAlgorithm.Sha256);
        spc.AddSource(CreateFunctionEmitter.GetHintName(model), sourceText);
    }
}
