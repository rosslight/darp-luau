using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class GeneratedExportsValidation
{
    public static void ValidateTypeShape(
        DiscoveredExportType discoveredType,
        GeneratedExportsCompilationContext context,
        List<Diagnostic> diagnostics
    )
    {
        ValidateType(discoveredType, context, diagnostics);
    }

    public static ValidatedExportType ValidateMembers(
        DiscoveredExportType discoveredType,
        NormalizedExportType normalizedType,
        List<Diagnostic> diagnostics
    )
    {
        ReportDuplicateNames(discoveredType.Symbol, normalizedType.Kind, normalizedType.Members, diagnostics);

        ValidatedLibraryExportNode? libraryRoot =
            normalizedType.Kind == LuauExportedTypeKind.Library ? BuildLibraryTree(normalizedType, diagnostics) : null;
        return new ValidatedExportType(normalizedType, libraryRoot);
    }

    private static void ValidateType(
        DiscoveredExportType discoveredType,
        GeneratedExportsCompilationContext context,
        List<Diagnostic> diagnostics
    )
    {
        if (!GeneratedExportsCompilationContext.IsPartial(discoveredType.Symbol))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    discoveredType.Kind == LuauExportedTypeKind.Library
                        ? DiagnosticDescriptors.LuauLibraryTypeMustBePartialDescriptor
                        : DiagnosticDescriptors.LuauUserdataTypeMustBePartialDescriptor,
                    discoveredType.Origin.Location
                )
            );
        }

        if (discoveredType.Kind == LuauExportedTypeKind.Library)
        {
            if (discoveredType.Symbol.ContainingType is not null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                        discoveredType.Origin.Location,
                        "nested library types are not supported in v1"
                    )
                );
            }

            if (discoveredType.Symbol.TypeParameters.Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                        discoveredType.Origin.Location,
                        "generic library types are not supported in v1"
                    )
                );
            }

            if (HasGeneratedLibraryMemberNameConflict(discoveredType.Symbol))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                        discoveredType.Origin.Location,
                        "library types cannot declare members named 'Register' or 'LuauLibraryName' because those names are generated"
                    )
                );
            }

            if (discoveredType.Symbol.TypeKind == TypeKind.Struct)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InstanceLibraryStructNotSupportedDescriptor,
                        discoveredType.Origin.Location
                    )
                );
            }

            return;
        }

        if (discoveredType.Symbol.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    discoveredType.Origin.Location,
                    "types marked with [LuauUserdata] must be classes"
                )
            );
        }

        if (context.ImplementsManualUserdataHooks(discoveredType.Symbol))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedUserdataManualInteropConflictDescriptor,
                    discoveredType.Origin.Location,
                    discoveredType.Symbol.Name
                )
            );
        }
    }

    private static bool HasGeneratedLibraryMemberNameConflict(INamedTypeSymbol type)
    {
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;

            if (member.Name is "Register" or "LuauLibraryName")
                return true;
        }

        return false;
    }

    private static void ReportDuplicateNames(
        INamedTypeSymbol type,
        LuauExportedTypeKind exportedTypeKind,
        ImmutableEquatableArray<NormalizedExportMember> members,
        List<Diagnostic> diagnostics
    )
    {
        var names = new Dictionary<string, NormalizedExportMember>(StringComparer.Ordinal);
        foreach (NormalizedExportMember member in members)
        {
            if (names.TryGetValue(member.LuauName, out _))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateLuauMemberNameDescriptor,
                        GeneratedExportsCompilationContext.GetSymbolLocation(member.Symbol),
                        member.LuauName,
                        exportedTypeKind == LuauExportedTypeKind.Library ? "library" : "userdata",
                        type.Name
                    )
                );
                continue;
            }

            names.Add(member.LuauName, member);
        }
    }

    private static ValidatedLibraryExportNode BuildLibraryTree(
        NormalizedExportType normalizedType,
        List<Diagnostic> diagnostics
    )
    {
        var root = new MutableLibraryExportNode(string.Empty);
        foreach (NormalizedExportMember member in normalizedType.Members)
            AddLibraryMember(root, normalizedType.Symbol, member, diagnostics);

        return Freeze(root);
    }

    private static void AddLibraryMember(
        MutableLibraryExportNode root,
        INamedTypeSymbol type,
        NormalizedExportMember member,
        List<Diagnostic> diagnostics
    )
    {
        MutableLibraryExportNode current = root;
        for (int i = 0; i < member.PathSegments.Length; i++)
        {
            string segment = member.PathSegments[i];
            bool isLeaf = i == member.PathSegments.Length - 1;
            if (!current.Children.TryGetValue(segment, out MutableLibraryExportNode? child))
            {
                child = new MutableLibraryExportNode(segment);
                current.Children.Add(segment, child);
            }

            if (isLeaf)
            {
                if (child.Children.Count > 0)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.LuauExportPathConflictDescriptor,
                            GeneratedExportsCompilationContext.GetSymbolLocation(member.Symbol),
                            member.LuauName,
                            GetNamespaceConflictPath(child, member.LuauName)
                        )
                    );
                    return;
                }

                if (child.Member is not null)
                    return;

                child.Member = member;
                return;
            }

            if (child.Member is not null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.LuauExportPathConflictDescriptor,
                        GeneratedExportsCompilationContext.GetSymbolLocation(member.Symbol),
                        child.Member.LuauName,
                        member.LuauName
                    )
                );
                return;
            }

            current = child;
        }
    }

    private static string GetNamespaceConflictPath(MutableLibraryExportNode node, string prefix)
    {
        if (node.Member is not null)
            return node.Member.LuauName;

        foreach (KeyValuePair<string, MutableLibraryExportNode> child in node.Children)
            return GetNamespaceConflictPath(child.Value, prefix + "." + child.Key);

        return prefix;
    }

    private static ValidatedLibraryExportNode Freeze(MutableLibraryExportNode node)
    {
        return new ValidatedLibraryExportNode(
            node.Name,
            node.Member,
            node.Children.Values.Select(Freeze).ToImmutableEquatableArray()
        );
    }

    private sealed class MutableLibraryExportNode(string name)
    {
        public string Name { get; } = name;

        public NormalizedExportMember? Member { get; set; }

        public Dictionary<string, MutableLibraryExportNode> Children { get; } = new(StringComparer.Ordinal);
    }
}
