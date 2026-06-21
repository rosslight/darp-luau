using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Darp.Luau.Generator.CreateFunction;

internal static class CreateFunctionDiscovery
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return node
            is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CreateFunction" }
                    or IdentifierNameSyntax { Identifier.ValueText: "CreateFunction" }
            };
    }

    public static IInvocationOperation? GetMatchingInvocation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
            return null;

        var apiSymbols = LuauApiSymbols.Create(context.SemanticModel.Compilation);
        return apiSymbols is not null && apiSymbols.IsCreateFunctionMethod(operation.TargetMethod) ? operation : null;
    }
}
