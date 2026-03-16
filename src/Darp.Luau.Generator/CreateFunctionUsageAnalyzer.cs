using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Darp.Luau.Generator;

/// <summary> An analyzer to catch invalid usage of CreateFunction{TDelegate}(TDelegate) </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CreateFunctionUsageAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.DirectInvocationRequiredDescriptor];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            INamedTypeSymbol? luauState = compilationContext.Compilation.GetTypeByMetadataName("Darp.Luau.LuauState");
            INamedTypeSymbol? delegateType = compilationContext.Compilation.GetTypeByMetadataName("System.Delegate");
            if (luauState is null || delegateType is null)
                return;

            IMethodSymbol? createFunction = luauState
                .GetMembers("CreateFunction")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.Parameters is [{ Type: ITypeParameterSymbol { ConstraintTypes.Length: 1 } typeSymbol }]
                    && SymbolEqualityComparer.Default.Equals(typeSymbol.ConstraintTypes[0], delegateType)
                );
            if (createFunction is null)
                return;

            compilationContext.RegisterOperationAction(
                context => AnalyzeMethodReference(context, createFunction),
                OperationKind.MethodReference
            );
        });
    }

    private static void AnalyzeMethodReference(OperationAnalysisContext context, IMethodSymbol createFunction)
    {
        var operation = (IMethodReferenceOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(operation.Method.OriginalDefinition, createFunction))
            return;

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.DirectInvocationRequiredDescriptor,
            operation.Syntax.GetLocation(),
            "Converting it to a delegate would hit the runtime stub."
        );
        context.ReportDiagnostic(diagnostic);
    }
}
