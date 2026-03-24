using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator;

internal static class LuauInteropTypeMapper
{
    public static bool TryMapManagedUserdataType(ITypeSymbol type, out string? typeName)
    {
        typeName = null;
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class } namedType)
            return false;

        foreach (INamedTypeSymbol implementedInterface in namedType.AllInterfaces)
        {
            if (
                implementedInterface is not { Name: "ILuauUserData", Arity: 1, TypeArguments.Length: 1 }
                || implementedInterface.ContainingNamespace.ToDisplayString() != "Darp.Luau"
            )
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(implementedInterface.TypeArguments[0], namedType))
                continue;

            typeName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        return false;
    }

    public static bool TryMapType(ITypeSymbol type, out LuauTypeMapping mapping)
    {
        bool isNullable = type.NullableAnnotation is NullableAnnotation.Annotated;
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } namedNullableType
        )
        {
            if (!TryMapType(namedNullableType.TypeArguments[0], out mapping))
                return false;

            mapping = mapping with { IsNullable = true };
            return true;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            mapping = new LuauTypeMapping(
                LuauValueType.Enum,
                isNullable,
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            );
            return true;
        }

        if (TryMapManagedUserdataType(type, out string? userdataTypeName))
        {
            mapping = new LuauTypeMapping(LuauValueType.ManagedUserdata, isNullable, userdataTypeName);
            return true;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                mapping = new LuauTypeMapping(LuauValueType.Boolean, isNullable, null);
                return true;
            case SpecialType.System_String:
                mapping = new LuauTypeMapping(LuauValueType.StringString, isNullable, null);
                return true;
            case SpecialType.System_Byte:
                mapping = new LuauTypeMapping(LuauValueType.NumberByte, isNullable, null);
                return true;
            case SpecialType.System_UInt16:
                mapping = new LuauTypeMapping(LuauValueType.NumberUShort, isNullable, null);
                return true;
            case SpecialType.System_UInt32:
                mapping = new LuauTypeMapping(LuauValueType.NumberUInt, isNullable, null);
                return true;
            case SpecialType.System_UInt64:
                mapping = new LuauTypeMapping(LuauValueType.NumberULong, isNullable, null);
                return true;
            case SpecialType.System_SByte:
                mapping = new LuauTypeMapping(LuauValueType.NumberSByte, isNullable, null);
                return true;
            case SpecialType.System_Int16:
                mapping = new LuauTypeMapping(LuauValueType.NumberShort, isNullable, null);
                return true;
            case SpecialType.System_Int32:
                mapping = new LuauTypeMapping(LuauValueType.NumberInt, isNullable, null);
                return true;
            case SpecialType.System_Int64:
                mapping = new LuauTypeMapping(LuauValueType.NumberLong, isNullable, null);
                return true;
            case SpecialType.System_Double:
                mapping = new LuauTypeMapping(LuauValueType.Number, isNullable, null);
                return true;
            case SpecialType.System_Single:
                mapping = new LuauTypeMapping(LuauValueType.NumberFloat, isNullable, null);
                return true;
            case SpecialType.System_Decimal:
                mapping = new LuauTypeMapping(LuauValueType.NumberDecimal, isNullable, null);
                return true;
        }

        switch (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
        {
            case "global::System.ReadOnlySpan<byte>":
                mapping = new LuauTypeMapping(LuauValueType.String, isNullable, null);
                return true;
            case "global::System.ReadOnlySpan<char>":
                mapping = new LuauTypeMapping(LuauValueType.StringCharSpan, isNullable, null);
                return true;
            case "global::System.Half":
                mapping = new LuauTypeMapping(LuauValueType.NumberHalf, isNullable, null);
                return true;
            case "global::System.UInt128":
                mapping = new LuauTypeMapping(LuauValueType.NumberUInt128, isNullable, null);
                return true;
            case "global::System.Int128":
                mapping = new LuauTypeMapping(LuauValueType.NumberInt128, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauValue":
                mapping = new LuauTypeMapping(LuauValueType.LuauValue, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauTableView":
                mapping = new LuauTypeMapping(LuauValueType.LuauTableView, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauFunctionView":
                mapping = new LuauTypeMapping(LuauValueType.LuauFunctionView, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauStringView":
                mapping = new LuauTypeMapping(LuauValueType.LuauStringView, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauBufferView":
                mapping = new LuauTypeMapping(LuauValueType.LuauBufferView, isNullable, null);
                return true;
            case "global::Darp.Luau.LuauUserdataView":
                mapping = new LuauTypeMapping(LuauValueType.LuauUserdataView, isNullable, null);
                return true;
            default:
                mapping = default;
                return false;
        }
    }

    public static bool SupportsNullableValue(LuauValueType type)
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
                or LuauValueType.Enum
                or LuauValueType.ManagedUserdata;
    }

    public static bool SupportsUsage(LuauTypeMapping mapping, LuauInteropTypeUsage usage)
    {
        return usage switch
        {
            LuauInteropTypeUsage.LibraryProperty
            or LuauInteropTypeUsage.UserdataPropertyGet
            or LuauInteropTypeUsage.UserdataPropertySet
            or LuauInteropTypeUsage.TypeFile => SupportsStoredPropertyType(mapping.Type),
            _ => true,
        };
    }

    private static bool SupportsStoredPropertyType(LuauValueType type)
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
                or LuauValueType.Enum
                or LuauValueType.ManagedUserdata;
    }
}
