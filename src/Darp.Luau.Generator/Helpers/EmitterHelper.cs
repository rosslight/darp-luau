namespace Darp.Luau.Generator.Helpers;

internal static class EmitterHelper
{
    internal static string GetDotnetType(InteropType param)
    {
        if (param is { Type: LuauInteropKind.Enum or LuauInteropKind.ManagedUserdata, OriginalTypeName: { } name })
            return param.IsNullable ? $"{name}?" : name;
        return GetDotnetType(param.Type, param.IsNullable);
    }

    internal static string GetTupleReturnType(InteropType param, int index)
    {
        string type = GetDotnetType(param);
        string defaultName = $"Item{index}";
        return param.TupleElementName is { Length: > 0 } name && name != defaultName ? $"{type} {name}" : type;
    }

    internal static string GetDotnetType((LuauInteropKind Type, bool IsNullable) tuple) =>
        GetDotnetType(tuple.Type, tuple.IsNullable);

    private static string GetDotnetType(LuauInteropKind type, bool isNullable)
    {
        string dotnetType = type switch
        {
            LuauInteropKind.Boolean => "bool",
            LuauInteropKind.String => "global::System.ReadOnlySpan<byte>",
            LuauInteropKind.StringCharSpan => "global::System.ReadOnlySpan<char>",
            LuauInteropKind.StringString => "string",
            LuauInteropKind.Number => "double",
            LuauInteropKind.NumberByte => "byte",
            LuauInteropKind.NumberUShort => "ushort",
            LuauInteropKind.NumberUInt => "uint",
            LuauInteropKind.NumberULong => "ulong",
            LuauInteropKind.NumberUInt128 => "global::System.UInt128",
            LuauInteropKind.NumberSByte => "sbyte",
            LuauInteropKind.NumberShort => "short",
            LuauInteropKind.NumberInt => "int",
            LuauInteropKind.NumberLong => "long",
            LuauInteropKind.NumberInt128 => "global::System.Int128",
            LuauInteropKind.NumberHalf => "global::System.Half",
            LuauInteropKind.NumberFloat => "float",
            LuauInteropKind.NumberDecimal => "decimal",
            LuauInteropKind.LuauValue => "global::Darp.Luau.LuauValue",
            LuauInteropKind.LuauTableView => "global::Darp.Luau.LuauTableView",
            LuauInteropKind.LuauFunctionView => "global::Darp.Luau.LuauFunctionView",
            LuauInteropKind.LuauStringView => "global::Darp.Luau.LuauStringView",
            LuauInteropKind.LuauBufferView => "global::Darp.Luau.LuauBufferView",
            LuauInteropKind.LuauUserdataView => "global::Darp.Luau.LuauUserdataView",
            LuauInteropKind.Enum or LuauInteropKind.ManagedUserdata => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Use GetDotnetType(ParameterTypeInfo) for enum and managed userdata types"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Could not get the dotnet type"),
        };
        return isNullable ? $"{dotnetType}?" : dotnetType;
    }

    public static string GetFunctionRepresentation(InteropSignature signature)
    {
        ImmutableEquatableArray<InteropType> parameters = signature.Parameters;
        ImmutableEquatableArray<InteropType> returnParameters = signature.ReturnTypes;
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
