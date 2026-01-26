using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Darp.Luau.Generator;

internal static class CreateFunctionInterceptorsEmitter
{
    private static ITypeSymbol? ExtractDelegateType(IInvocationOperation invocation, SemanticModel semanticModel)
    {
        if (invocation.Arguments.IsEmpty)
            return null;

        var argument = invocation.Arguments[0];
        return argument.Value.Type;
    }

    private static InvocationMethodSignature ExtractSignature(
        ITypeSymbol? delegateType,
        SemanticModel semanticModel,
        List<Diagnostic> diagnostics
    )
    {
        if (delegateType is not INamedTypeSymbol namedType)
        {
            return new InvocationMethodSignature(
                ImmutableArray<LuauValueType>.Empty,
                ImmutableArray<LuauValueType>.Empty
            );
        }

        var parameters = new List<LuauValueType>();
        var returnTypes = new List<LuauValueType>();

        // Handle Action<T1, T2, ...>
        if (namedType.Name == "Action")
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                var luauType = MapTypeToLuauValueType(typeArg, semanticModel, diagnostics);
                parameters.Add(luauType);
            }
        }
        // Handle Func<T1, T2, ..., TResult>
        else if (namedType.Name == "Func")
        {
            var typeArgs = namedType.TypeArguments;
            if (typeArgs.Length > 0)
            {
                // All but the last are parameters
                for (int i = 0; i < typeArgs.Length - 1; i++)
                {
                    var luauType = MapTypeToLuauValueType(typeArgs[i], semanticModel, diagnostics);
                    parameters.Add(luauType);
                }
                // Last is return type
                var returnType = MapTypeToLuauValueType(typeArgs[^1], semanticModel, diagnostics);
                returnTypes.Add(returnType);
            }
        }

        return new InvocationMethodSignature(parameters.ToImmutableArray(), returnTypes.ToImmutableArray());
    }

    private static LuauValueType MapTypeToLuauValueType(
        ITypeSymbol type,
        SemanticModel semanticModel,
        List<Diagnostic> diagnostics
    )
    {
        if (type.SpecialType == SpecialType.System_String)
            return LuauValueType.String;

        if (type.SpecialType == SpecialType.System_Int32)
            return LuauValueType.Int32;

        // Report unsupported type diagnostic
        var location = type.Locations.IsEmpty ? Location.None : type.Locations[0];
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedTypeDescriptor,
            location,
            type.ToDisplayString()
        );
        diagnostics.Add(diagnostic);

        // Fallback to String for unsupported types
        return LuauValueType.String;
    }

    private static string GenerateFunctionBody(InvocationMethodSignature signature)
    {
        var paramExtractions = new List<string>();
        var callParameters = new List<string>();

        // Generate parameter extraction for each parameter
        for (int i = 0; i < signature.Parameters.Length; i++)
        {
            LuauValueType paramType = signature.Parameters[i];
            int paramIndex = i + 1;
            string varName = $"v{paramIndex}";

            if (paramType == LuauValueType.String)
            {
                paramExtractions.Add(
                    $"string {varName} = global::System.Text.Encoding.UTF8.GetString(x.CheckString(parameterIndex: {paramIndex}));"
                );
            }
            else if (paramType == LuauValueType.Int32)
            {
                // TODO: CheckInt32 method needs to be implemented in LuauFunctions
                paramExtractions.Add(
                    $"int {varName} = throw new global::System.NotImplementedException(\"Int32 parameter extraction not yet implemented\");"
                );
            }

            callParameters.Add(varName);
        }

        string callExpression =
            callParameters.Count == 0 ? "onLuaCall();" : $"onLuaCall({string.Join(", ", callParameters)});";

        string body = $$"""
            void F(LuauFunctions x)
            {
                global::System.ArgumentOutOfRangeException.ThrowIfNotEqual(x.NumberOfParameters, {{signature.Parameters.Length}});
                {{string.Join("\n    ", paramExtractions)}}
                {{callExpression}}
            }
            """;

        return body;
    }

    public static bool TryEmit(
        ImmutableArray<IInvocationOperation> calls,
        [NotNullWhen(true)] out string? source,
        out List<Diagnostic> diagnostics
    )
    {
        using var stringWriter = new StringWriter();
        using var writer = new IndentedTextWriter(stringWriter);
        diagnostics = [];

        var groups = new Dictionary<InvocationMethodSignature, HashSet<InterceptorLocationData>>(
            InvocationSignatureComparer.Instance
        );
        foreach (IInvocationOperation calledMethod in calls)
        {
            var syntax = (InvocationExpressionSyntax)calledMethod.Syntax;
            InterceptableLocation? location = calledMethod.SemanticModel.GetInterceptableLocation(syntax);
            if (location is null || calledMethod.SemanticModel is null)
                continue;

            ITypeSymbol? delegateType = ExtractDelegateType(calledMethod, calledMethod.SemanticModel);
            InvocationMethodSignature key = ExtractSignature(delegateType, calledMethod.SemanticModel, diagnostics);

            if (!groups.TryGetValue(key, out HashSet<InterceptorLocationData> locations))
                groups.Add(key, locations = []);
            locations.Add(new InterceptorLocationData(location.Version, location.Data));
        }

        if (groups.Count == 0)
        {
            source = null;
            return false;
        }

        writer.WriteMultiLine(
            """
            // <auto-generated/>
            #nullable enable

            namespace Darp.Luau.Generator
            {
            file static class CreateFunctionInterceptors
            {
            """
        );
        writer.Indent++;
        foreach (KeyValuePair<InvocationMethodSignature, HashSet<InterceptorLocationData>> group in groups)
        {
            IEnumerable<string> locations = group.Value.Select(x =>
                $"""[global::System.Runtime.CompilerServices.InterceptsLocationAttribute({x.Version}, "{x.Data}")]"""
            );

            InvocationMethodSignature signature = group.Key;
            string callFunction = EmitterHelper.GetFunctionRepresentation(signature);
            string functionBody = GenerateFunctionBody(signature);

            writer.WriteMultiLine(
                $$"""
                {{string.Join("\n", locations)}}
                public static global::Darp.Luau.LuauFunction CreateMethod(this global::Darp.Luau.LuauState state, {{callFunction}} onLuaCall)
                {
                """
            );
            writer.Indent++;
            writer.WriteMultiLine(
                $"""
                global::System.ArgumentNullException.ThrowIfNull(state);
                global::System.ArgumentNullException.ThrowIfNull(onLuaCall);
                return state.CreateFunction(F);

                {functionBody}
                """
            );
            writer.Indent--;
            writer.WriteLine("}");
        }

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteMultiLine(
            """
            namespace System.Runtime.CompilerServices
            {
                [global::System.Diagnostics.Conditional("DEBUG")]
                [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                file sealed class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(int version, string data)
                    {
                        _ = version;
                        _ = data;
                    }
                }
            }
            """
        );
        source = stringWriter.ToString();
        return true;
    }
}

internal readonly record struct InterceptorLocationData(int Version, string Data);

internal readonly record struct InvocationMethodSignature(
    ImmutableArray<LuauValueType> Parameters,
    ImmutableArray<LuauValueType> ReturnParameters
);

internal sealed class InvocationSignatureComparer : IEqualityComparer<InvocationMethodSignature>
{
    public static readonly InvocationSignatureComparer Instance = new();

    public bool Equals(InvocationMethodSignature x, InvocationMethodSignature y) =>
        SequenceEqual(x.Parameters, y.Parameters) && SequenceEqual(x.ReturnParameters, y.ReturnParameters);

    public int GetHashCode(InvocationMethodSignature obj)
    {
        unchecked
        {
            int hash = 17;

            // Parameters
            hash = (hash * 31) + obj.Parameters.Length;
            for (int i = 0; i < obj.Parameters.Length; i++)
                hash = (hash * 31) + (int)obj.Parameters[i];

            // Separator to reduce accidental collisions between params/returns
            hash = (int)(hash * 31 + 0x9E3779B9);

            // ReturnParameters
            hash = (hash * 31) + obj.ReturnParameters.Length;
            for (int i = 0; i < obj.ReturnParameters.Length; i++)
                hash = (hash * 31) + (int)obj.ReturnParameters[i];

            return hash;
        }
    }

    private static bool SequenceEqual(ImmutableArray<LuauValueType> a, ImmutableArray<LuauValueType> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }
}
