using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.Helpers;

internal sealed class LuauApiSymbols
{
    private readonly INamedTypeSymbol _delegateTypeSymbol;

    private LuauApiSymbols(
        INamedTypeSymbol luauStateSymbol,
        INamedTypeSymbol delegateTypeSymbol,
        INamedTypeSymbol libraryAttributeSymbol,
        INamedTypeSymbol userdataAttributeSymbol,
        INamedTypeSymbol memberAttributeSymbol,
        INamedTypeSymbol luauUserdataInterfaceSymbol
    )
    {
        LuauStateSymbol = luauStateSymbol;
        _delegateTypeSymbol = delegateTypeSymbol;
        LibraryAttributeSymbol = libraryAttributeSymbol;
        UserdataAttributeSymbol = userdataAttributeSymbol;
        MemberAttributeSymbol = memberAttributeSymbol;
        LuauUserdataInterfaceSymbol = luauUserdataInterfaceSymbol;
    }

    public INamedTypeSymbol LuauStateSymbol { get; }

    public INamedTypeSymbol LibraryAttributeSymbol { get; }

    public INamedTypeSymbol UserdataAttributeSymbol { get; }

    public INamedTypeSymbol MemberAttributeSymbol { get; }

    public INamedTypeSymbol LuauUserdataInterfaceSymbol { get; }

    public static LuauApiSymbols? Create(Compilation compilation)
    {
        INamedTypeSymbol? luauStateSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauState");
        INamedTypeSymbol? delegateTypeSymbol = compilation.GetTypeByMetadataName("System.Delegate");
        INamedTypeSymbol? libraryAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauLibraryAttribute");
        INamedTypeSymbol? userdataAttributeSymbol = compilation.GetTypeByMetadataName(
            "Darp.Luau.LuauUserdataAttribute"
        );
        INamedTypeSymbol? memberAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauMemberAttribute");
        INamedTypeSymbol? luauUserdataInterfaceSymbol = compilation.GetTypeByMetadataName("Darp.Luau.ILuauUserData`1");
        if (
            luauStateSymbol is null
            || delegateTypeSymbol is null
            || libraryAttributeSymbol is null
            || userdataAttributeSymbol is null
            || memberAttributeSymbol is null
            || luauUserdataInterfaceSymbol is null
        )
        {
            return null;
        }

        return new LuauApiSymbols(
            luauStateSymbol,
            delegateTypeSymbol,
            libraryAttributeSymbol,
            userdataAttributeSymbol,
            memberAttributeSymbol,
            luauUserdataInterfaceSymbol
        );
    }

    public bool IsCreateFunctionMethod(IMethodSymbol method)
    {
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, LuauStateSymbol))
            return false;

        return method.OriginalDefinition
                is {
                    Name: "CreateFunction",
                    Parameters: [{ Type: ITypeParameterSymbol { ConstraintTypes.Length: 1 } typeSymbol }],
                }
            && SymbolEqualityComparer.Default.Equals(typeSymbol.ConstraintTypes[0], _delegateTypeSymbol);
    }

    public AttributeData? GetLibraryAttribute(ISymbol symbol) =>
        AttributeReader.GetAttribute(symbol, LibraryAttributeSymbol);

    public AttributeData? GetUserdataAttribute(ISymbol symbol) =>
        AttributeReader.GetAttribute(symbol, UserdataAttributeSymbol);

    public AttributeData? GetMemberAttribute(ISymbol symbol) =>
        AttributeReader.GetAttribute(symbol, MemberAttributeSymbol);

    public bool ImplementsManualUserdataHooks(INamedTypeSymbol type) => type.ImplementsManualUserdataHooks(this);
}
