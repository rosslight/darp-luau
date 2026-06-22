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
        IncrementalValuesProvider<CreateFunctionModel> models = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: CreateFunctionDiscovery.IsCandidate,
                transform: AnalyzeCandidate
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName("CreateFunctionModels");

        context.RegisterSourceOutput(models, Emit);
    }

    private static CreateFunctionModel? AnalyzeCandidate(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        IInvocationOperation? invocation = CreateFunctionDiscovery.GetMatchingInvocation(context, cancellationToken);
        return invocation is null ? null : CreateFunctionSignatureAnalyzer.Analyze(invocation).Model;
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
