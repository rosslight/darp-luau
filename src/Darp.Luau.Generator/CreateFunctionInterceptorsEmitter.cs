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
        signature = default;
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
        // Delegate inference can erase nullable reference returns for plain lambdas, so
        // merge in lambda-body-derived nullability when it is available.
        ImmutableArray<LambdaReturnOverride> returnOverrides =
            LambdaReturnNullabilityResolver.GetReturnOverrides(invocationOperation);

        for (int i = 0; i < invokeMethod.Parameters.Length; i++)
        {
            var typeArg = invokeMethod.Parameters[i];
            Location parameterLocation = GetParameterLocation(invocationOperation, i);
            string usageDescription = typeArg.Name is { Length: > 0 }
                ? $"parameter '{typeArg.Name}'"
                : $"parameter #{i + 1}";

            if (
                !TryMapTypeToParameterTypeInfo(
                    typeArg.Type,
                    parameterLocation,
                    usageDescription,
                    LuauInteropTypeUsage.CreateFunctionParameter,
                    out ParameterTypeInfo parameterTypeInfo,
                    diagnostics
                )
            )
            {
                return false;
            }

            if (typeArg.NullableAnnotation is NullableAnnotation.Annotated)
                parameterTypeInfo = parameterTypeInfo with { IsNullable = true };

            if (parameterTypeInfo.IsNullable && !LuauInteropTypeMapper.SupportsNullableValue(parameterTypeInfo.Type))
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

            parameters.Add(parameterTypeInfo);
        }
        if (
            !TryExtractReturnParameters(
                invocationOperation,
                invokeMethod.ReturnType,
                invokeMethod.ReturnNullableAnnotation,
                returnOverrides,
                returnTypes,
                diagnostics
            )
        )
            return false;

        signature = new InvocationMethodSignature(
            parameters.ToImmutableEquatableArray(),
            returnTypes.ToImmutableEquatableArray()
        );
        return true;
    }

    private static bool TryMapTypeToParameterTypeInfo(
        ITypeSymbol type,
        Location diagnosticLocation,
        string usageDescription,
        LuauInteropTypeUsage usage,
        out ParameterTypeInfo parameterTypeInfo,
        List<Diagnostic> diagnostics
    )
    {
        if (!LuauInteropTypeMapper.TryMapType(type, out LuauTypeMapping mapping))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    diagnosticLocation,
                    type.ToDisplayString(),
                    usageDescription
                )
            );
            parameterTypeInfo = default;
            return false;
        }

        if (!LuauInteropTypeMapper.SupportsUsage(mapping, usage))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    diagnosticLocation,
                    type.ToDisplayString(),
                    usageDescription
                )
            );
            parameterTypeInfo = default;
            return false;
        }

        if (mapping.IsNullable && !LuauInteropTypeMapper.SupportsNullableValue(mapping.Type))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    diagnosticLocation,
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    usageDescription
                )
            );
            parameterTypeInfo = default;
            return false;
        }

        parameterTypeInfo = new ParameterTypeInfo(mapping.Type, mapping.IsNullable, mapping.OriginalTypeName, null);
        return true;
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

    private static bool TryExtractReturnParameters(
        IInvocationOperation invocationOperation,
        ITypeSymbol returnType,
        NullableAnnotation returnNullableAnnotation,
        ImmutableArray<LambdaReturnOverride> returnOverrides,
        ImmutableArray<ParameterTypeInfo>.Builder returnTypes,
        List<Diagnostic> diagnostics
    )
    {
        if (returnType.SpecialType is SpecialType.System_Void)
            return true;

        Location returnLocation = GetReturnLocation(invocationOperation);
        if (returnType is not INamedTypeSymbol { IsTupleType: true } tupleType)
            return TryExtractSingleReturnParameter(
                returnType,
                returnLocation,
                "the return value",
                returnTypes,
                diagnostics,
                nullableAnnotationOverride: returnNullableAnnotation,
                nullableOverride: returnOverrides is [var singleOverride] ? singleOverride.IsNullable : null
            );

        if (tupleType.TupleElements.Length > 4)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedReturnTupleDescriptor,
                    returnLocation,
                    tupleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "only up to 4 tuple return values are supported"
                )
            );
            return false;
        }

        for (int i = 0; i < tupleType.TupleElements.Length; i++)
        {
            IFieldSymbol tupleElement = tupleType.TupleElements[i];
            if (tupleElement.Type is INamedTypeSymbol { IsTupleType: true } nestedTupleType)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedReturnTupleDescriptor,
                        returnLocation,
                        tupleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        $"nested tuple element '{nestedTupleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' is not supported"
                    )
                );
                return false;
            }

            string usageDescription = tupleElement.Name is { Length: > 0 }
                ? $"return tuple element '{tupleElement.Name}'"
                : "a return tuple element";
            if (
                !TryExtractSingleReturnParameter(
                    tupleElement.Type,
                    returnLocation,
                    usageDescription,
                    returnTypes,
                    diagnostics,
                    tupleElement.NullableAnnotation,
                    tupleElement.Name,
                    returnOverrides.Length > i ? returnOverrides[i].IsNullable : null
                )
            )
                return false;
        }

        return true;
    }

    private static bool TryExtractSingleReturnParameter(
        ITypeSymbol returnType,
        Location returnLocation,
        string returnUsageDescription,
        ImmutableArray<ParameterTypeInfo>.Builder returnTypes,
        List<Diagnostic> diagnostics,
        NullableAnnotation? nullableAnnotationOverride = null,
        string? tupleElementName = null,
        bool? nullableOverride = null
    )
    {
        if (
            !TryMapTypeToParameterTypeInfo(
                returnType,
                returnLocation,
                returnUsageDescription,
                LuauInteropTypeUsage.CreateFunctionReturn,
                out ParameterTypeInfo mappedReturnParameter,
                diagnostics
            )
        )
        {
            return false;
        }

        LuauValueType mappedReturnType = mappedReturnParameter.Type;
        bool isReturnNullable = mappedReturnParameter.IsNullable;

        if (nullableAnnotationOverride is NullableAnnotation.Annotated)
            isReturnNullable = true;

        // Prefer the lambda-body override when Roslyn inferred a less precise delegate signature.
        if (nullableOverride is true)
            isReturnNullable = true;

        if (isReturnNullable && !LuauInteropTypeMapper.SupportsNullableValue(mappedReturnType))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTypeDescriptor,
                    returnLocation,
                    returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    returnUsageDescription
                )
            );
            return false;
        }

        returnTypes.Add(
            new ParameterTypeInfo(mappedReturnType, isReturnNullable, mappedReturnParameter.OriginalTypeName, tupleElementName)
        );
        return true;
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
            LuauValueType.LuauFunctionView => "TryReadLuauFunction",
            LuauValueType.LuauBufferView => "TryReadLuauBuffer",
            LuauValueType.LuauUserdataView => "TryReadLuauUserdata",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the TryRead function"),
        };
    }

    private static string GenerateCheckParameter(int parameterIndex, ParameterTypeInfo param)
    {
        string dotnetType = GetDotnetType(param);

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
            LuauValueType.ManagedUserdata => param.IsNullable
                ? $"""
                    if (!args.TryReadUserdataOrNil<{param.OriginalTypeName}>(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadUserdata<{param.OriginalTypeName}>(parameterIndex: {parameterIndex}, out {dotnetType}? a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.LuauValue
            or LuauValueType.LuauTableView
            or LuauValueType.LuauStringView
            or LuauValueType.LuauFunctionView
            or LuauValueType.LuauBufferView
            or LuauValueType.LuauUserdataView => $"""
                if (!args.{GetTryFunction(
                    param.Type
                )}(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
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
        if (signature.ReturnParameters.Length == 0)
        {
            writer.WriteLine($"{callExpression};");
            writer.WriteLine("return global::Darp.Luau.LuauReturn.Ok();");
        }
        else
        {
            writer.WriteLine($"var returns = {callExpression};");
            if (signature.ReturnParameters.Length == 1)
            {
                writer.WriteLine(
                    $"return global::Darp.Luau.LuauReturn.Ok({FormatReturnValueExpression("returns", signature.ReturnParameters[0])});"
                );
            }
            else
            {
                writer.WriteLine(
                    $"return global::Darp.Luau.LuauReturn.Ok({string.Join(", ", signature.ReturnParameters.Select((x, i) => FormatReturnValueExpression($"returns.Item{i + 1}", x)))});"
                );
            }
        }
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static string FormatReturnValueExpression(string valueExpression, ParameterTypeInfo returnParameter)
    {
        return returnParameter.Type switch
        {
            LuauValueType.NumberDecimal or LuauValueType.NumberUInt128 or LuauValueType.NumberInt128 =>
                returnParameter.IsNullable ? $"(double?){valueExpression}" : $"(double){valueExpression}",
            LuauValueType.Enum => returnParameter.IsNullable
                ? $"(double?){valueExpression}"
                : $"(double){valueExpression}",
            LuauValueType.ManagedUserdata => returnParameter.IsNullable
                ? $"{valueExpression} is null ? default(global::Darp.Luau.IntoLuau) : global::Darp.Luau.IntoLuau.FromUserdata({valueExpression})"
                : $"global::Darp.Luau.IntoLuau.FromUserdata({valueExpression})",
            _ => valueExpression,
        };
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

        var groups = new Dictionary<InvocationMethodSignature, HashSet<InterceptorLocationData>>();
        foreach (IInvocationOperation calledMethod in calls)
        {
            var syntax = (InvocationExpressionSyntax)calledMethod.Syntax;
            InterceptableLocation? location = calledMethod.SemanticModel.GetInterceptableLocation(syntax);
            if (calledMethod.SemanticModel is null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InterceptableLocationUnavailableDescriptor,
                        syntax.GetLocation(),
                        "the semantic model was unavailable"
                    )
                );
                continue;
            }

            if (location is null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InterceptableLocationUnavailableDescriptor,
                        syntax.GetLocation(),
                        "the compiler did not provide an interceptable location for this invocation"
                    )
                );
                continue;
            }

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

internal readonly record struct ParameterTypeInfo(
    LuauValueType Type,
    bool IsNullable,
    string? OriginalTypeName,
    string? TupleElementName
);

internal readonly record struct InvocationMethodSignature(
    ImmutableEquatableArray<ParameterTypeInfo> Parameters,
    ImmutableEquatableArray<ParameterTypeInfo> ReturnParameters
);
