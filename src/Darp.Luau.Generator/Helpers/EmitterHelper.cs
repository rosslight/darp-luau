using System.Collections.Immutable;

namespace Darp.Luau.Generator.Helpers;

internal static class EmitterHelper
{
    internal static string GetDotnetType(ParameterTypeInfo param)
    {
        if (param is { Type: LuauValueType.Enum or LuauValueType.ManagedUserdata, OriginalTypeName: { } name })
            return param.IsNullable ? $"{name}?" : name;
        return GetDotnetType(param.Type, param.IsNullable);
    }

    internal static string GetTupleReturnType(ParameterTypeInfo param, int index)
    {
        string type = GetDotnetType(param);
        string defaultName = $"Item{index}";
        return param.TupleElementName is { Length: > 0 } name && name != defaultName ? $"{type} {name}" : type;
    }

    internal static string GetDotnetType((LuauValueType Type, bool IsNullable) tuple) =>
        GetDotnetType(tuple.Type, tuple.IsNullable);

    private static string GetDotnetType(LuauValueType type, bool isNullable)
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
            LuauValueType.LuauTableView => "global::Darp.Luau.LuauTableView",
            LuauValueType.LuauFunctionView => "global::Darp.Luau.LuauFunctionView",
            LuauValueType.LuauStringView => "global::Darp.Luau.LuauStringView",
            LuauValueType.LuauBufferView => "global::Darp.Luau.LuauBufferView",
            LuauValueType.LuauUserdataView => "global::Darp.Luau.LuauUserdataView",
            LuauValueType.Enum or LuauValueType.ManagedUserdata => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Use GetDotnetType(ParameterTypeInfo) for enum and managed userdata types"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the dotnet type"),
        };
        return isNullable ? $"{dotnetType}?" : dotnetType;
    }

    public static string GetFunctionRepresentation(InvocationMethodSignature signature)
    {
        ImmutableArray<ParameterTypeInfo> parameters = signature.Parameters;
        ImmutableArray<ParameterTypeInfo> returnParameters = signature.ReturnParameters;
        return (parameters.Length, returnParameters.Length) switch
        {
            (0, 0) => "global::System.Action",
            (_, 0) => $"global::System.Action<{string.Join(",", parameters.Select(GetDotnetType))}>",
            (0, 1) => $"global::System.Func<{GetDotnetType(returnParameters[0])}>",
            (0, _) =>
                $"global::System.Func<({string.Join(", ", returnParameters.Select((x, i) => GetTupleReturnType(x, i + 1)))})>",
            (_, 1) =>
                $"global::System.Func<{string.Join(",", parameters.Select(GetDotnetType))}, {GetDotnetType(returnParameters[0])}>",
            (_, _) =>
                $"global::System.Func<{string.Join(",", parameters.Select(GetDotnetType))}, ({string.Join(", ", returnParameters.Select((x, i) => GetTupleReturnType(x, i + 1)))})>",
        };
    }
}
