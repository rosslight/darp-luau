using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Darp.Luau.Generator.GeneratedExports;

internal sealed class GeneratedExportsCompilationContext
{
    public INamedTypeSymbol LibraryAttributeSymbol { get; }

    public INamedTypeSymbol UserdataAttributeSymbol { get; }

    public INamedTypeSymbol MemberAttributeSymbol { get; }

    public INamedTypeSymbol LuauUserdataInterfaceSymbol { get; }

    private GeneratedExportsCompilationContext(
        INamedTypeSymbol libraryAttributeSymbol,
        INamedTypeSymbol userdataAttributeSymbol,
        INamedTypeSymbol memberAttributeSymbol,
        INamedTypeSymbol luauUserdataInterfaceSymbol
    )
    {
        LibraryAttributeSymbol = libraryAttributeSymbol;
        UserdataAttributeSymbol = userdataAttributeSymbol;
        MemberAttributeSymbol = memberAttributeSymbol;
        LuauUserdataInterfaceSymbol = luauUserdataInterfaceSymbol;
    }

    public static GeneratedExportsCompilationContext? Create(Compilation compilation)
    {
        INamedTypeSymbol? libraryAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauLibraryAttribute");
        INamedTypeSymbol? userdataAttributeSymbol = compilation.GetTypeByMetadataName(
            "Darp.Luau.LuauUserdataAttribute"
        );
        INamedTypeSymbol? memberAttributeSymbol = compilation.GetTypeByMetadataName("Darp.Luau.LuauMemberAttribute");
        INamedTypeSymbol? luauUserdataInterfaceSymbol = compilation.GetTypeByMetadataName("Darp.Luau.ILuauUserData`1");
        if (
            libraryAttributeSymbol is null
            || userdataAttributeSymbol is null
            || memberAttributeSymbol is null
            || luauUserdataInterfaceSymbol is null
        )
        {
            return null;
        }

        return new GeneratedExportsCompilationContext(
            libraryAttributeSymbol,
            userdataAttributeSymbol,
            memberAttributeSymbol,
            luauUserdataInterfaceSymbol
        );
    }

    public AttributeData? GetLibraryAttribute(ISymbol symbol) => GetAttribute(symbol, LibraryAttributeSymbol);

    public AttributeData? GetUserdataAttribute(ISymbol symbol) => GetAttribute(symbol, UserdataAttributeSymbol);

    public AttributeData? GetMemberAttribute(ISymbol symbol) => GetAttribute(symbol, MemberAttributeSymbol);

    public bool ImplementsManualUserdataHooks(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, LuauUserdataInterfaceSymbol)
        );
    }

    public static string? GetStringConstructorArgument(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        return attribute.ConstructorArguments[0].Value as string;
    }

    public static bool IsPartial(INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Length > 0
            && type.DeclaringSyntaxReferences.Select(static x => x.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .All(static x => x.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
    }

    public static Location GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? GetSymbolLocation(fallbackSymbol);
    }

    public static Location GetSymbolLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        return symbol
            .GetAttributes()
            .FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol)
            );
    }
}
