using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class GeneratedExportsIrProjector
{
    public static GeneratedExportSurfaceIr Project(ValidatedExportType validatedType)
    {
        return new GeneratedExportSurfaceIr(
            validatedType.Type.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            validatedType.Type.Kind,
            validatedType.Type.LibraryName,
            validatedType.Type.Members.Select(ProjectMember).ToImmutableEquatableArray(),
            validatedType.LibraryRoot is null ? null : ProjectNode(validatedType.LibraryRoot)
        );
    }

    private static GeneratedExportMemberIr ProjectMember(NormalizedExportMember member)
    {
        return member switch
        {
            NormalizedExportPropertyMember property => new GeneratedExportPropertyIr(
                property.ManagedName,
                property.LuauName,
                property.PathSegments,
                property.Property.Getter is null ? null : new GeneratedExportAccessorIr(property.Property.Getter.Type),
                property.Property.Setter is null ? null : new GeneratedExportAccessorIr(property.Property.Setter.Type)
            ),
            NormalizedExportMethodMember method => new GeneratedExportMethodIr(
                method.ManagedName,
                method.LuauName,
                method.PathSegments,
                method.Method.Parameters,
                method.Method.ReturnTypes
            ),
            _ => throw new InvalidOperationException($"Unsupported normalized member type '{member.GetType().Name}'")
        };
    }

    private static GeneratedLibraryExportNodeIr ProjectNode(ValidatedLibraryExportNode node)
    {
        return new GeneratedLibraryExportNodeIr(
            node.Name,
            node.Member is null ? null : ProjectMember(node.Member),
            node.Children.Select(ProjectNode).ToImmutableEquatableArray()
        );
    }
}
