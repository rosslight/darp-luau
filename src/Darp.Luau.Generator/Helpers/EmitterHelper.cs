using System.Collections.Immutable;

namespace Darp.Luau.Generator.Helpers;

internal static class EmitterHelper
{
    internal static string GetDotnetType((LuauValueType Type, bool IsNullable) tuple) =>
        GetDotnetType(tuple.Type, tuple.IsNullable);

    internal static string GetDotnetType(LuauValueType type, bool isNullable)
    {
        string dotnetType = type switch
        {
            LuauValueType.Boolean => "bool",
            LuauValueType.String => "global::System.ReadOnlySpan<byte>",
            LuauValueType.StringCharSpan => "global::System.ReadOnlySpan<char>",
            LuauValueType.StringString => "string",
            LuauValueType.Number => "double",
            LuauValueType.NumberByte => "byte",
            LuauValueType.NumberUShort => "ushort",
            LuauValueType.NumberUInt => "uint",
            LuauValueType.NumberULong => "ulong",
            LuauValueType.NumberUInt128 => "global::System.UInt128",
            LuauValueType.NumberSByte => "sbyte",
            LuauValueType.NumberShort => "short",
            LuauValueType.NumberInt => "int",
            LuauValueType.NumberLong => "long",
            LuauValueType.NumberInt128 => "global::System.Int128",
            LuauValueType.NumberHalf => "global::System.Half",
            LuauValueType.NumberFloat => "float",
            LuauValueType.NumberDecimal => "decimal",
            LuauValueType.LuauValue => "global::Darp.Luau.LuauValue",
            LuauValueType.LuauTable => "global::Darp.Luau.LuauTable",
            LuauValueType.LuauFunction => "global::Darp.Luau.LuauFunction",
            LuauValueType.LuauString => "global::Darp.Luau.LuauString",
            LuauValueType.LuauBuffer => "global::System.ReadOnlySpan<byte>",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the dotnet type"),
        };
        return isNullable ? $"{dotnetType}?" : dotnetType;
    }

    public static string GetFunctionRepresentation(InvocationMethodSignature signature)
    {
        ImmutableArray<(LuauValueType Type, bool IsNullable)> parameters = signature.Parameters;
        ImmutableArray<(LuauValueType Type, bool IsNullable)> returnParameters = signature.ReturnParameters;
        return (parameters.Length, returnParameters.Length) switch
        {
            (0, 0) => "global::System.Action",
            (_, 0) => $"global::System.Action<{string.Join(",", parameters.Select(GetDotnetType))}>",
            (0, 1) => $"global::System.Func<{GetDotnetType(returnParameters[0])}>",
            (0, _) => $"global::System.Func<({string.Join("\n", returnParameters.Select(GetDotnetType))})>",
            (_, 1) =>
                $"global::System.Func<{string.Join(",", parameters.Select(GetDotnetType))}, {GetDotnetType(returnParameters[0])}>",
            (_, _) =>
                $"global::System.Func<{string.Join(",", parameters.Select(GetDotnetType))}, ({string.Join("\n", returnParameters.Select(GetDotnetType))})>",
        };
    }
}
