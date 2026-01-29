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
    private static ITypeSymbol? ExtractDelegateType(IInvocationOperation invocation, SemanticModel semanticModel)
    {
        if (invocation.Arguments.IsEmpty)
            return null;

        var argument = invocation.Arguments[0];
        return argument.Value.Type;
    }

    private static bool TryExtractSignature(
        ITypeSymbol? delegateType,
        out InvocationMethodSignature signature,
        List<Diagnostic> diagnostics
    )
    {
        if (delegateType is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
        {
            // TODO: Diagnostic error about invalid delegate type
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<(LuauValueType Type, bool IsNullable)>();
        var returnTypes = ImmutableArray.CreateBuilder<(LuauValueType Type, bool IsNullable)>();

        foreach (var typeArg in invokeMethod.Parameters)
        {
            if (!TryMapTypeToLuauValueType(typeArg.Type, out LuauValueType luauType, out bool isNullable, diagnostics))
                return false;
            parameters.Add((luauType, isNullable));
        }
        // Last is return type
        if (invokeMethod.ReturnType.SpecialType is not SpecialType.System_Void)
        {
            if (
                !TryMapTypeToLuauValueType(
                    invokeMethod.ReturnType,
                    out var returnType,
                    out bool isReturnNullable,
                    diagnostics
                )
            )
            {
                return false;
            }
            returnTypes.Add((returnType, isReturnNullable));
        }

        signature = new InvocationMethodSignature(parameters.ToImmutableArray(), returnTypes.ToImmutableArray());
        return true;
    }

    private static bool TryMapTypeToLuauValueType(
        ITypeSymbol type,
        out LuauValueType luauType,
        out bool isNullable,
        List<Diagnostic> diagnostics
    )
    {
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
            if (!TryMapTypeToLuauValueType(namedType.TypeArguments[0], out luauType, out _, diagnostics))
            {
                return false;
            }
            isNullable = true;
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
                luauType = LuauValueType.LuauTable;
                return true;
            case "global::Darp.Luau.LuauFunction":
                luauType = LuauValueType.LuauFunction;
                return true;
            case "global::Darp.Luau.LuauString":
                luauType = LuauValueType.LuauString;
                return true;
        }

        // Report unsupported type diagnostic
        var location = type.Locations.IsEmpty ? Location.None : type.Locations[0];
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedTypeDescriptor,
            location,
            type.ToDisplayString()
        );
        diagnostics.Add(diagnostic);
        luauType = default;
        return false;
    }

    private static string GetCheckFunction(LuauValueType type)
    {
        return type switch
        {
            LuauValueType.Boolean => "CheckBoolean",
            LuauValueType.String or LuauValueType.StringCharSpan or LuauValueType.StringString => "CheckString",
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
            or LuauValueType.NumberDecimal => "CheckNumber",
            LuauValueType.LuauValue => "CheckLuauValue",
            LuauValueType.LuauString => "CheckLuauString",
            LuauValueType.LuauTable => "CheckLuauTable",
            LuauValueType.LuauFunction => "CheckLuauFunction",
            LuauValueType.LuauBuffer => "CheckLuauBuffer",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the Check function"),
        };
    }

    private static string GenerateCheckParameter(int parameterIndex, LuauValueType type, bool isNullable)
    {
        string dotnetType = GetDotnetType(type, isNullable);
        string dotnetCheckFunction = isNullable ? $"{GetCheckFunction(type)}OrNil" : GetCheckFunction(type);

        return type switch
        {
            LuauValueType.Boolean
            or LuauValueType.String
            or LuauValueType.Number
            or LuauValueType.LuauValue
            or LuauValueType.LuauTable
            or LuauValueType.LuauString
            or LuauValueType.LuauFunction =>
                $"{dotnetType} v{parameterIndex} = x.{dotnetCheckFunction}(parameterIndex: {parameterIndex});",
            LuauValueType.StringCharSpan => $"""
                global::System.ReadOnlySpan<byte> v{parameterIndex}Lua = x.{dotnetCheckFunction}(parameterIndex: {parameterIndex});
                global::System.Span<char> v{parameterIndex} = stackalloc char[global::System.Text.Encoding.UTF8.GetCharCount(v{parameterIndex}Lua)];
                _ = global::System.Text.Encoding.UTF8.TryGetChars(v{parameterIndex}Lua, v{parameterIndex}, out _);
                """,
            LuauValueType.StringString => isNullable
                ? $"""
                    global::System.ReadOnlySpan<byte> v{parameterIndex}Lua = x.CheckStringOrNil(parameterIndex: {parameterIndex}, out bool v{parameterIndex}IsNull);
                    {dotnetType} v{parameterIndex} = v{parameterIndex}IsNull ? null : global::System.Text.Encoding.UTF8.GetString(v{parameterIndex}Lua);
                    """
                : $"""
                    global::System.ReadOnlySpan<byte> v{parameterIndex}Lua = x.CheckString(parameterIndex: {parameterIndex});
                    string v{parameterIndex} = global::System.Text.Encoding.UTF8.GetString(v{parameterIndex}Lua);
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
            or LuauValueType.NumberDecimal => isNullable
                ? $"""
                    double? v{parameterIndex}Lua = x.CheckNumberOrNil(parameterIndex: {parameterIndex});
                    {dotnetType} v{parameterIndex} = ({dotnetType})v{parameterIndex}Lua;
                    """
                : $"""
                    double v{parameterIndex}Lua = x.CheckNumber(parameterIndex: {parameterIndex});
                    {dotnetType} v{parameterIndex} = ({dotnetType})v{parameterIndex}Lua;
                    """,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not generate the Check parameter"),
        };
    }

    private static void EmitFunctionBody(IndentedTextWriter writer, InvocationMethodSignature signature)
    {
        string[] paramExtractions = signature
            .Parameters.Select((paramType, i) => GenerateCheckParameter(i + 1, paramType.Type, paramType.IsNullable))
            .ToArray();

        string callExpression =
            $"onLuaCall({string.Join(", ", Enumerable.Range(1, paramExtractions.Length).Select(x => $"v{x}"))});";

        writer.WriteLine("void F(ref LuauFunctions x)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine(
            $"global::System.ArgumentOutOfRangeException.ThrowIfNotEqual(x.NumberOfParameters, {signature.Parameters.Length});"
        );
        writer.WriteMultiLine(string.Join("\n", paramExtractions));
        if (!signature.ReturnParameters.IsEmpty)
        {
            writer.WriteLine($"var returns = {callExpression}");
            if (
                signature.ReturnParameters[0].Type
                is LuauValueType.NumberDecimal
                    or LuauValueType.NumberUInt128
                    or LuauValueType.NumberInt128
            )
            {
                string type = signature.ReturnParameters[0].IsNullable ? "double?" : "double";
                writer.WriteLine($"x.ReturnParameter(({type})returns);");
            }
            else
            {
                writer.WriteLine("x.ReturnParameter(returns);");
            }
        }
        else
        {
            writer.WriteLine(callExpression);
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

            ITypeSymbol? delegateType = ExtractDelegateType(calledMethod, calledMethod.SemanticModel);
            if (!TryExtractSignature(delegateType, out var key, diagnostics))
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

internal readonly record struct InvocationMethodSignature(
    ImmutableArray<(LuauValueType Type, bool IsNullable)> Parameters,
    ImmutableArray<(LuauValueType Type, bool IsNullable)> ReturnParameters
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
            }

            // Separator to reduce accidental collisions between params/returns
            hash = (int)(hash * 31 + 0x9E3779B9);

            // ReturnParameters
            hash = (hash * 31) + obj.ReturnParameters.Length;
            for (int i = 0; i < obj.ReturnParameters.Length; i++)
            {
                hash = (hash * 31) + (int)obj.ReturnParameters[i].Type;
                hash = (hash * 31) + (obj.ReturnParameters[i].IsNullable ? 1 : 0);
            }

            return hash;
        }
    }

    private static bool SequenceEqual(
        ImmutableArray<(LuauValueType Type, bool IsNullable)> a,
        ImmutableArray<(LuauValueType Type, bool IsNullable)> b
    )
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }
}
