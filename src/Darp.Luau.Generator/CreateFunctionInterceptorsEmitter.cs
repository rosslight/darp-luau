using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Darp.Luau.Generator.Helpers.EmitterHelper;

namespace Darp.Luau.Generator;

internal static class CreateFunctionInterceptorsEmitter
{
    private static bool TryExtractSignature(
        IInvocationOperation invocationOperation,
        out InvocationMethodSignature signature,
        List<Diagnostic> diagnostics
    )
    {
        ITypeSymbol? delegateType = invocationOperation.Arguments.Select(x => x.Value.Type).FirstOrDefault();

        if (delegateType is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
        {
            var syntax = (InvocationExpressionSyntax)invocationOperation.Syntax;
            Location diagnosticLocation = syntax.GetLocation();

            GenericNameSyntax? genericName = syntax.Expression switch
            {
                GenericNameSyntax directGeneric => directGeneric,
                MemberAccessExpressionSyntax { Name: GenericNameSyntax memberGeneric } => memberGeneric,
                _ => null,
            };
            if (genericName?.TypeArgumentList.Arguments.Count > 0)
            {
                // If the user has specified an explicit type that is invalid, flag this
                diagnosticLocation = genericName.TypeArgumentList.Arguments[0].GetLocation();
            }
            else if (syntax.ArgumentList.Arguments.Count > 0)
            {
                // Fallback to argument location when no explicit type arguments
                ArgumentSyntax argSyntax = syntax.ArgumentList.Arguments[0];
                diagnosticLocation = argSyntax.Expression.GetLocation();
            }

            string typeDisplayName = delegateType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "null";
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InvalidDelegateTypeDescriptor,
                diagnosticLocation,
                typeDisplayName
            );
            diagnostics.Add(diagnostic);
            signature = default;
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterTypeInfo>();
        var returnTypes = ImmutableArray.CreateBuilder<ParameterTypeInfo>();

        for (int i = 0; i < invokeMethod.Parameters.Length; i++)
        {
            var typeArg = invokeMethod.Parameters[i];
            Location parameterLocation = GetParameterLocation(invocationOperation, i);
            string usageDescription = typeArg.Name is { Length: > 0 }
                ? $"parameter '{typeArg.Name}'"
                : $"parameter #{i + 1}";

            if (
                !TryMapTypeToLuauValueType(
                    typeArg.Type,
                    parameterLocation,
                    isParameter: true,
                    usageDescription,
                    out LuauValueType luauType,
                    out bool isNullable,
                    out string? originalTypeName,
                    diagnostics
                )
            )
            {
                return false;
            }

            if (isNullable && !SupportsNullableParameter(luauType))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    parameterLocation,
                    typeArg.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    usageDescription
                );
                diagnostics.Add(diagnostic);
                return false;
            }

            parameters.Add(new ParameterTypeInfo(luauType, isNullable, originalTypeName));
        }
        // Last is return type
        if (invokeMethod.ReturnType.SpecialType is not SpecialType.System_Void)
        {
            Location returnLocation = GetReturnLocation(invocationOperation);
            const string returnUsageDescription = "the return value";

            if (
                !TryMapTypeToLuauValueType(
                    invokeMethod.ReturnType,
                    returnLocation,
                    isParameter: false,
                    returnUsageDescription,
                    out LuauValueType returnType,
                    out bool isReturnNullable,
                    out string? returnOriginalTypeName,
                    diagnostics
                )
            )
            {
                return false;
            }

            if (isReturnNullable && !SupportsNullableParameter(returnType))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    returnLocation,
                    invokeMethod.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    returnUsageDescription
                );
                diagnostics.Add(diagnostic);
                return false;
            }

            returnTypes.Add(new ParameterTypeInfo(returnType, isReturnNullable, returnOriginalTypeName));
        }

        signature = new InvocationMethodSignature(parameters.ToImmutableArray(), returnTypes.ToImmutableArray());
        return true;
    }

    private static bool SupportsNullableParameter(LuauValueType type)
    {
        return type
            is LuauValueType.Boolean
                or LuauValueType.StringString
                or LuauValueType.Number
                or LuauValueType.NumberByte
                or LuauValueType.NumberUShort
                or LuauValueType.NumberUInt
                or LuauValueType.NumberULong
                or LuauValueType.NumberUInt128
                or LuauValueType.NumberSByte
                or LuauValueType.NumberShort
                or LuauValueType.NumberInt
                or LuauValueType.NumberLong
                or LuauValueType.NumberInt128
                or LuauValueType.NumberHalf
                or LuauValueType.NumberFloat
                or LuauValueType.NumberDecimal
                or LuauValueType.Enum;
    }

    private static bool TryMapTypeToLuauValueType(
        ITypeSymbol type,
        Location diagnosticLocation,
        bool isParameter,
        string usageDescription,
        out LuauValueType luauType,
        out bool isNullable,
        out string? originalTypeName,
        List<Diagnostic> diagnostics
    )
    {
        originalTypeName = null;
        isNullable = type.NullableAnnotation is NullableAnnotation.Annotated;
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } namedType
        )
        {
            // Unwrap the nullable type and recursively check the underlying type
            if (
                !TryMapTypeToLuauValueType(
                    namedType.TypeArguments[0],
                    diagnosticLocation,
                    isParameter,
                    usageDescription,
                    out luauType,
                    out _,
                    out originalTypeName,
                    diagnostics
                )
            )
            {
                return false;
            }
            isNullable = true;
            return true;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            luauType = LuauValueType.Enum;
            originalTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                luauType = LuauValueType.Boolean;
                return true;
            case SpecialType.System_String:
                luauType = LuauValueType.StringString;
                return true;
            case SpecialType.System_Byte:
                luauType = LuauValueType.NumberByte;
                return true;
            case SpecialType.System_UInt16:
                luauType = LuauValueType.NumberUShort;
                return true;
            case SpecialType.System_UInt32:
                luauType = LuauValueType.NumberUInt;
                return true;
            case SpecialType.System_UInt64:
                luauType = LuauValueType.NumberULong;
                return true;
            case SpecialType.System_SByte:
                luauType = LuauValueType.NumberSByte;
                return true;
            case SpecialType.System_Int16:
                luauType = LuauValueType.NumberShort;
                return true;
            case SpecialType.System_Int32:
                luauType = LuauValueType.NumberInt;
                return true;
            case SpecialType.System_Int64:
                luauType = LuauValueType.NumberLong;
                return true;
            case SpecialType.System_Double:
                luauType = LuauValueType.Number;
                return true;
            case SpecialType.System_Single:
                luauType = LuauValueType.NumberFloat;
                return true;
            case SpecialType.System_Decimal:
                luauType = LuauValueType.NumberDecimal;
                return true;
        }
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        switch (name)
        {
            case "global::System.ReadOnlySpan<byte>":
                luauType = LuauValueType.String;
                return true;
            case "global::System.ReadOnlySpan<char>":
                luauType = LuauValueType.StringCharSpan;
                return true;
            case "global::System.Half":
                luauType = LuauValueType.NumberHalf;
                return true;
            case "global::System.UInt128":
                luauType = LuauValueType.NumberUInt128;
                return true;
            case "global::System.Int128":
                luauType = LuauValueType.NumberInt128;
                return true;
            case "global::Darp.Luau.LuauValue":
                luauType = LuauValueType.LuauValue;
                return true;
            case "global::Darp.Luau.LuauTable":
                if (isParameter)
                    break;
                luauType = LuauValueType.LuauTable;
                return true;
            case "global::Darp.Luau.LuauTableView":
                luauType = LuauValueType.LuauTableView;
                return true;
            case "global::Darp.Luau.LuauFunctionView":
                luauType = LuauValueType.LuauFunctionView;
                return true;
            case "global::Darp.Luau.LuauStringView":
                luauType = LuauValueType.LuauStringView;
                return true;
            case "global::Darp.Luau.LuauBufferView":
                luauType = LuauValueType.LuauBufferView;
                return true;
            case "global::Darp.Luau.LuauUserdataView":
                luauType = LuauValueType.LuauUserdataView;
                return true;
        }

        // Report unsupported type diagnostic at the call site
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedTypeDescriptor,
            diagnosticLocation,
            type.ToDisplayString(),
            usageDescription
        );
        diagnostics.Add(diagnostic);
        luauType = default;
        return false;
    }

    private static Location GetParameterLocation(IInvocationOperation invocationOperation, int parameterIndex)
    {
        var syntax = (InvocationExpressionSyntax)invocationOperation.Syntax;

        if (syntax.ArgumentList.Arguments.Count == 0)
            return syntax.GetLocation();

        ExpressionSyntax delegateExpression = syntax.ArgumentList.Arguments[0].Expression;
        switch (delegateExpression)
        {
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                if (parameterIndex < parenthesizedLambda.ParameterList.Parameters.Count)
                {
                    var parameterSyntax = parenthesizedLambda.ParameterList.Parameters[parameterIndex];
                    return (parameterSyntax.Type ?? (SyntaxNode)parameterSyntax).GetLocation();
                }
                break;
            case SimpleLambdaExpressionSyntax simpleLambda when parameterIndex == 0:
            {
                ParameterSyntax parameterSyntax = simpleLambda.Parameter;
                return (parameterSyntax.Type ?? (SyntaxNode)parameterSyntax).GetLocation();
            }
        }
        return delegateExpression.GetLocation();
    }

    private static Location GetReturnLocation(IInvocationOperation invocationOperation)
    {
        var syntax = (InvocationExpressionSyntax)invocationOperation.Syntax;
        if (syntax.ArgumentList.Arguments.Count == 0)
            return syntax.GetLocation();

        ExpressionSyntax delegateExpression = syntax.ArgumentList.Arguments[0].Expression;
        switch (delegateExpression)
        {
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                if (parenthesizedLambda.ExpressionBody is { } exprBody)
                    return exprBody.GetLocation();

                if (parenthesizedLambda.Block is { } block)
                {
                    foreach (var statement in block.Statements)
                    {
                        if (statement is ReturnStatementSyntax { Expression: { } returnExpr })
                            return returnExpr.GetLocation();
                    }
                }
                break;
            case SimpleLambdaExpressionSyntax simpleLambda:
                if (simpleLambda.ExpressionBody is { } simpleExprBody)
                    return simpleExprBody.GetLocation();
                if (simpleLambda.Block is { } simpleBlock)
                {
                    foreach (var statement in simpleBlock.Statements)
                    {
                        if (statement is ReturnStatementSyntax { Expression: { } returnExpr })
                            return returnExpr.GetLocation();
                    }
                }
                break;
        }
        return delegateExpression.GetLocation();
    }

    private static string GetTryFunction(LuauValueType type)
    {
        return type switch
        {
            LuauValueType.Boolean => "TryReadBoolean",
            LuauValueType.String or LuauValueType.StringCharSpan or LuauValueType.StringString => "TryReadUtf8String",
            LuauValueType.Number
            or LuauValueType.NumberByte
            or LuauValueType.NumberUShort
            or LuauValueType.NumberUInt
            or LuauValueType.NumberULong
            or LuauValueType.NumberUInt128
            or LuauValueType.NumberSByte
            or LuauValueType.NumberShort
            or LuauValueType.NumberInt
            or LuauValueType.NumberLong
            or LuauValueType.NumberInt128
            or LuauValueType.NumberHalf
            or LuauValueType.NumberFloat
            or LuauValueType.NumberDecimal
            or LuauValueType.Enum => "TryReadNumber",
            LuauValueType.LuauValue => "TryReadLuauValue",
            LuauValueType.LuauStringView => "TryReadLuauString",
            LuauValueType.LuauTableView => "TryReadLuauTable",
            LuauValueType.LuauTable => "TryReadLuauTable",
            LuauValueType.LuauFunctionView => "TryReadLuauFunction",
            LuauValueType.LuauBufferView => "TryReadLuauBuffer",
            LuauValueType.LuauUserdataView => "TryReadLuauUserdata",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the TryRead function"),
        };
    }

    private static string GenerateCheckParameter(int parameterIndex, ParameterTypeInfo param)
    {
        string dotnetType = GetDotnetType(param);
        string tryFunctionName = GetTryFunction(param.Type);

        return param.Type switch
        {
            LuauValueType.Boolean => param.IsNullable
                ? $"""
                    if (!args.TryReadBooleanOrNil(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadBoolean(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.String => param.IsNullable
                ? $"""
                    if (!args.TryReadUtf8StringOrNil(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out var isNil, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadUtf8String(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.StringCharSpan => $"""
                if (!args.TryReadUtf8String(parameterIndex: {parameterIndex}, out global::System.ReadOnlySpan<byte> a{parameterIndex}Raw, out error))
                    return global::Darp.Luau.LuauReturn.Error(error);
                global::System.Span<char> a{parameterIndex} = stackalloc char[global::System.Text.Encoding.UTF8.GetCharCount(a{parameterIndex}Raw)];
                _ = global::System.Text.Encoding.UTF8.GetChars(a{parameterIndex}Raw, a{parameterIndex});
                """,
            LuauValueType.StringString => param.IsNullable
                ? $"""
                    if (!args.TryReadUtf8StringOrNil(parameterIndex: {parameterIndex}, out global::System.ReadOnlySpan<byte> a{parameterIndex}Raw, out bool a{parameterIndex}IsNil, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = a{parameterIndex}IsNil ? null : global::System.Text.Encoding.UTF8.GetString(a{parameterIndex}Raw);
                    """
                : $"""
                    if (!args.TryReadUtf8String(parameterIndex: {parameterIndex}, out global::System.ReadOnlySpan<byte> a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    string a{parameterIndex} = global::System.Text.Encoding.UTF8.GetString(a{parameterIndex}Raw);
                    """,
            LuauValueType.Number => param.IsNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadNumber(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.NumberByte
            or LuauValueType.NumberUShort
            or LuauValueType.NumberUInt
            or LuauValueType.NumberULong
            or LuauValueType.NumberUInt128
            or LuauValueType.NumberSByte
            or LuauValueType.NumberShort
            or LuauValueType.NumberInt
            or LuauValueType.NumberLong
            or LuauValueType.NumberInt128
            or LuauValueType.NumberHalf
            or LuauValueType.NumberFloat
            or LuauValueType.NumberDecimal => param.IsNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(parameterIndex: {parameterIndex}, out double? a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = ({dotnetType})a{parameterIndex}Raw;
                    """
                : $"""
                    if (!args.TryReadNumber(parameterIndex: {parameterIndex}, out double a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = ({dotnetType})a{parameterIndex}Raw;
                    """,
            LuauValueType.Enum => param.IsNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(parameterIndex: {parameterIndex}, out double? a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = a{parameterIndex}Raw.HasValue ? ({param.OriginalTypeName})a{parameterIndex}Raw.Value : null;
                    """
                : $"""
                    if (!args.TryReadNumber(parameterIndex: {parameterIndex}, out double a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = ({param.OriginalTypeName})a{parameterIndex}Raw;
                    """,
            LuauValueType.LuauValue
            or LuauValueType.LuauTableView
            or LuauValueType.LuauTable
            or LuauValueType.LuauStringView
            or LuauValueType.LuauFunctionView
            or LuauValueType.LuauBufferView
            or LuauValueType.LuauUserdataView => $"""
                if (!args.{tryFunctionName}(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                    return global::Darp.Luau.LuauReturn.Error(error);
                """,
            _ => throw new ArgumentOutOfRangeException(
                nameof(param),
                param.Type,
                "Could not generate the Check parameter"
            ),
        };
    }

    private static void EmitFunctionBody(IndentedTextWriter writer, InvocationMethodSignature signature)
    {
        string[] paramExtractions = signature
            .Parameters.Select((paramType, i) => GenerateCheckParameter(i + 1, paramType))
            .ToArray();

        string callExpression =
            $"onLuaCall({string.Join(", ", Enumerable.Range(1, paramExtractions.Length).Select(i => $"a{i}"))})";

        writer.WriteLine("global::Darp.Luau.LuauReturn F(global::Darp.Luau.LuauArgs args)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"if (!args.TryValidateArgumentCount({signature.Parameters.Length}, out string? error))");
        writer.WriteLine("    return global::Darp.Luau.LuauReturn.Error(error);");
        writer.WriteMultiLine(string.Join("\n", paramExtractions));
        if (!signature.ReturnParameters.IsEmpty)
        {
            writer.WriteLine($"var returns = {callExpression};");
            if (
                signature.ReturnParameters[0].Type
                is LuauValueType.NumberDecimal
                    or LuauValueType.NumberUInt128
                    or LuauValueType.NumberInt128
            )
            {
                string type = signature.ReturnParameters[0].IsNullable ? "double?" : "double";
                writer.WriteLine($"return global::Darp.Luau.LuauReturn.Ok(({type})returns);");
            }
            else if (signature.ReturnParameters[0].Type == LuauValueType.Enum)
            {
                string type = signature.ReturnParameters[0].IsNullable ? "double?" : "double";
                writer.WriteLine($"return global::Darp.Luau.LuauReturn.Ok(({type})returns);");
            }
            else
            {
                writer.WriteLine("return global::Darp.Luau.LuauReturn.Ok(returns);");
            }
        }
        else
        {
            writer.WriteLine($"{callExpression};");
            writer.WriteLine("return global::Darp.Luau.LuauReturn.Ok();");
        }
        writer.Indent--;
        writer.WriteLine("}");
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

            if (!TryExtractSignature(calledMethod, out var key, diagnostics))
                continue;

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
            string callFunction = GetFunctionRepresentation(signature);
            string opt = string.Join("", signature.Parameters.Select((x, i) => x.IsNullable ? $"Opt{i + 1}" : ""));
            string optReturn = string.Join(
                "",
                signature.ReturnParameters.Select((x, i) => x.IsNullable ? $"OptR{i + 1}" : "")
            );
            writer.WriteMultiLine(
                $$"""
                {{string.Join("\n", locations)}}
                public static global::Darp.Luau.LuauFunction CreateMethod{{opt}}{{optReturn}}(this global::Darp.Luau.LuauState state, {{callFunction}} onLuaCall)
                {
                """
            );
            writer.Indent++;
            writer.WriteMultiLine(
                """
                global::System.ArgumentNullException.ThrowIfNull(state);
                global::System.ArgumentNullException.ThrowIfNull(onLuaCall);
                return state.CreateFunctionBuilder(F);

                """
            );
            EmitFunctionBody(writer, signature);

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

internal readonly record struct ParameterTypeInfo(LuauValueType Type, bool IsNullable, string? OriginalTypeName);

internal readonly record struct InvocationMethodSignature(
    ImmutableArray<ParameterTypeInfo> Parameters,
    ImmutableArray<ParameterTypeInfo> ReturnParameters
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
            {
                hash = (hash * 31) + (int)obj.Parameters[i].Type;
                hash = (hash * 31) + (obj.Parameters[i].IsNullable ? 1 : 0);
                hash =
                    (hash * 31)
                    + (obj.Parameters[i].OriginalTypeName is { } n ? StringComparer.Ordinal.GetHashCode(n) : 0);
            }

            // Separator to reduce accidental collisions between params/returns
            hash = (int)(hash * 31 + 0x9E3779B9);

            // ReturnParameters
            hash = (hash * 31) + obj.ReturnParameters.Length;
            for (int i = 0; i < obj.ReturnParameters.Length; i++)
            {
                hash = (hash * 31) + (int)obj.ReturnParameters[i].Type;
                hash = (hash * 31) + (obj.ReturnParameters[i].IsNullable ? 1 : 0);
                hash =
                    (hash * 31)
                    + (obj.ReturnParameters[i].OriginalTypeName is { } n ? StringComparer.Ordinal.GetHashCode(n) : 0);
            }

            return hash;
        }
    }

    private static bool SequenceEqual(ImmutableArray<ParameterTypeInfo> a, ImmutableArray<ParameterTypeInfo> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }
}
