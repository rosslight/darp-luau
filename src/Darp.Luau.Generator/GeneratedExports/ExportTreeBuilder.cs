using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class ExportTreeBuilder
{
    public static ValidatedLibraryExportNode BuildLibraryTree(
        NormalizedExportType normalizedType,
        List<Diagnostic> diagnostics
    )
    {
        var root = new MutableLibraryExportNode(string.Empty);
        foreach (NormalizedExportMember member in normalizedType.Members)
            AddLibraryMember(root, member, diagnostics);

        return Freeze(root);
    }

    private static void AddLibraryMember(
        MutableLibraryExportNode root,
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
                            SymbolExtensions.GetSymbolLocation(member.Symbol),
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
                        SymbolExtensions.GetSymbolLocation(member.Symbol),
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
