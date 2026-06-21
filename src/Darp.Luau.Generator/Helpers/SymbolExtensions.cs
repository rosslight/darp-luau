using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Darp.Luau.Generator.Helpers;

internal static class SymbolExtensions
{
    public static bool IsPartial(this INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Length > 0
            && type.DeclaringSyntaxReferences.Select(static x => x.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .All(static x => x.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
    }

    public static bool ImplementsManualUserdataHooks(this INamedTypeSymbol type, LuauApiSymbols apiSymbols)
    {
        return type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, apiSymbols.LuauUserdataInterfaceSymbol)
        );
    }

    public static Location GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? GetSymbolLocation(fallbackSymbol);
    }

    public static Location GetSymbolLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }
}
