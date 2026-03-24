using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class GeneratedExportsDiscovery
{
    public static DiscoveredExportType? DiscoverType(
        INamedTypeSymbol type,
        GeneratedExportsCompilationContext context,
        List<Diagnostic> diagnostics
    )
    {
        AttributeData? libraryAttribute = context.GetLibraryAttribute(type);
        AttributeData? userdataAttribute = context.GetUserdataAttribute(type);
        if (libraryAttribute is null && userdataAttribute is null)
            return null;

        if (libraryAttribute is not null && userdataAttribute is not null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    GeneratedExportsCompilationContext.GetAttributeLocation(libraryAttribute, type),
                    $"type '{type.Name}' cannot be both a [LuauLibrary] and a [LuauUserdata]"
                )
            );
            return null;
        }

        AttributeData attribute = libraryAttribute ?? userdataAttribute!;
        LuauExportedTypeKind kind = libraryAttribute is not null ? LuauExportedTypeKind.Library : LuauExportedTypeKind.Userdata;

        var members = new List<DiscoveredExportMember>();
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;

            AttributeData? memberAttribute = context.GetMemberAttribute(member);
            if (memberAttribute is null)
                continue;

            SourceOrigin origin = new(
                member.Name,
                GeneratedExportsCompilationContext.GetAttributeLocation(memberAttribute, member)
            );
            switch (member)
            {
                case IPropertySymbol property:
                    members.Add(new DiscoveredExportProperty(property, memberAttribute, origin));
                    break;
                case IMethodSymbol method:
                    members.Add(new DiscoveredExportMethod(method, memberAttribute, origin));
                    break;
            }
        }

        return new DiscoveredExportType(
            type,
            kind,
            attribute,
            new SourceOrigin(
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                GeneratedExportsCompilationContext.GetAttributeLocation(attribute, type)
            ),
            members.ToImmutableEquatableArray()
        );
    }
}
