using Darp.Luau.Generator.Helpers;

namespace Darp.Luau.Generator;

internal static class LuauMarshallingEmitter
{
    public static string GenerateParameterRead(int parameterIndex, ParameterTypeInfo param)
    {
        return GenerateParameterRead(
            parameterIndex,
            param.Type,
            param.IsNullable,
            param.OriginalTypeName,
            EmitterHelper.GetDotnetType(param)
        );
    }

    public static string GenerateParameterRead(int parameterIndex, LuauTypeMapping param)
    {
        return GenerateParameterRead(
            parameterIndex,
            param.Type,
            param.IsNullable,
            param.OriginalTypeName,
            GetDotnetType(param)
        );
    }

    public static string GenerateSingleArgumentRead(string variableName, LuauTypeMapping param)
    {
        return GenerateSingleArgumentRead(variableName, param.Type, param.IsNullable, param.OriginalTypeName, GetDotnetType(param));
    }

    public static string FormatIntoLuauExpression(string valueExpression, ParameterTypeInfo parameter)
    {
        return FormatIntoLuauExpression(valueExpression, parameter.Type, parameter.IsNullable);
    }

    public static string FormatIntoLuauExpression(string valueExpression, LuauTypeMapping mapping)
    {
        return FormatIntoLuauExpression(valueExpression, mapping.Type, mapping.IsNullable);
    }

    private static string GenerateParameterRead(
        int parameterIndex,
        LuauValueType type,
        bool isNullable,
        string? originalTypeName,
        string dotnetType
    )
    {
        return type switch
        {
            LuauValueType.Boolean => isNullable
                ? $"""
                    if (!args.TryReadBooleanOrNil(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadBoolean(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.String => isNullable
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
            LuauValueType.StringString => isNullable
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
            LuauValueType.Number => isNullable
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
            or LuauValueType.NumberDecimal => isNullable
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
            LuauValueType.Enum => isNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(parameterIndex: {parameterIndex}, out double? a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = a{parameterIndex}Raw.HasValue ? ({originalTypeName})a{parameterIndex}Raw.Value : null;
                    """
                : $"""
                    if (!args.TryReadNumber(parameterIndex: {parameterIndex}, out double a{parameterIndex}Raw, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    {dotnetType} a{parameterIndex} = ({originalTypeName})a{parameterIndex}Raw;
                    """,
            LuauValueType.ManagedUserdata => isNullable
                ? $"""
                    if (!args.TryReadUserdataOrNil<{originalTypeName}>(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """
                : $"""
                    if (!args.TryReadUserdata<{originalTypeName}>(parameterIndex: {parameterIndex}, out {dotnetType}? a{parameterIndex}, out error))
                        return global::Darp.Luau.LuauReturn.Error(error);
                    """,
            LuauValueType.LuauValue
            or LuauValueType.LuauTableView
            or LuauValueType.LuauStringView
            or LuauValueType.LuauFunctionView
            or LuauValueType.LuauBufferView
            or LuauValueType.LuauUserdataView =>
                $"""
                if (!args.{GetTryFunction(type)}(parameterIndex: {parameterIndex}, out {dotnetType} a{parameterIndex}, out error))
                    return global::Darp.Luau.LuauReturn.Error(error);
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not generate parameter reads"),
        };
    }

    private static string GenerateSingleArgumentRead(
        string variableName,
        LuauValueType type,
        bool isNullable,
        string? originalTypeName,
        string dotnetType
    )
    {
        return type switch
        {
            LuauValueType.Boolean => isNullable
                ? $"""
                    if (!args.TryReadBooleanOrNil(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """
                : $"""
                    if (!args.TryReadBoolean(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """,
            LuauValueType.StringString => isNullable
                ? $"""
                    if (!args.TryReadUtf8StringOrNil(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """
                : $"""
                    if (!args.TryReadUtf8String(out {dotnetType}? {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """,
            LuauValueType.Number => isNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """
                : $"""
                    if (!args.TryReadNumber(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
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
                    if (!args.TryReadNumberOrNil(out double? {variableName}Raw, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    {dotnetType} {variableName} = ({dotnetType}){variableName}Raw;
                    """
                : $"""
                    if (!args.TryReadNumber(out double {variableName}Raw, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    {dotnetType} {variableName} = ({dotnetType}){variableName}Raw;
                    """,
            LuauValueType.Enum => isNullable
                ? $"""
                    if (!args.TryReadNumberOrNil(out double? {variableName}Raw, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    {dotnetType} {variableName} = {variableName}Raw.HasValue ? ({originalTypeName}){variableName}Raw.Value : null;
                    """
                : $"""
                    if (!args.TryReadNumber(out double {variableName}Raw, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    {dotnetType} {variableName} = ({originalTypeName}){variableName}Raw;
                    """,
            LuauValueType.ManagedUserdata => isNullable
                ? $"""
                    if (!args.TryReadUserdataOrNil<{originalTypeName}>(out {dotnetType} {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """
                : $"""
                    if (!args.TryReadUserdata<{originalTypeName}>(out {dotnetType}? {variableName}, out string? error))
                        return global::Darp.Luau.LuauOutcome.Error(error);
                    """,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not generate single argument reads"),
        };
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
            LuauValueType.LuauTableView => "TryReadLuauTable",
            LuauValueType.LuauFunctionView => "TryReadLuauFunction",
            LuauValueType.LuauStringView => "TryReadLuauString",
            LuauValueType.LuauBufferView => "TryReadLuauBuffer",
            LuauValueType.LuauUserdataView => "TryReadLuauUserdata",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the TryRead function"),
        };
    }

    private static string FormatIntoLuauExpression(string valueExpression, LuauValueType type, bool isNullable)
    {
        return type switch
        {
            LuauValueType.NumberDecimal or LuauValueType.NumberUInt128 or LuauValueType.NumberInt128 => isNullable
                ? $"(double?){valueExpression}"
                : $"(double){valueExpression}",
            LuauValueType.Enum => isNullable ? $"(double?){valueExpression}" : $"(double){valueExpression}",
            LuauValueType.ManagedUserdata => isNullable
                ? $"{valueExpression} is null ? default(global::Darp.Luau.IntoLuau) : global::Darp.Luau.IntoLuau.FromUserdata({valueExpression})"
                : $"global::Darp.Luau.IntoLuau.FromUserdata({valueExpression})",
            _ => valueExpression,
        };
    }

    private static string GetDotnetType(LuauTypeMapping param)
    {
        if (param is { Type: LuauValueType.Enum or LuauValueType.ManagedUserdata, OriginalTypeName: { } name })
            return param.IsNullable ? $"{name}?" : name;

        return EmitterHelper.GetDotnetType((param.Type, param.IsNullable));
    }
}
