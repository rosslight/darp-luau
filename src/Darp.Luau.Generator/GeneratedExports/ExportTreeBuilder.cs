using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class ExportTreeBuilder
{
    public static ValidatedModuleExportNode BuildModuleTree(
        NormalizedExportType normalizedType,
        List<Diagnostic> diagnostics
    )
    {
        var root = new MutableModuleExportNode(string.Empty);
        foreach (NormalizedExportMember member in normalizedType.Members)
            AddModuleMember(root, member, diagnostics);

        return Freeze(root);
    }

    private static void AddModuleMember(
        MutableModuleExportNode root,
        NormalizedExportMember member,
        List<Diagnostic> diagnostics
    )
    {
        MutableModuleExportNode current = root;
        for (int i = 0; i < member.PathSegments.Length; i++)
        {
            string segment = member.PathSegments[i];
            bool isLeaf = i == member.PathSegments.Length - 1;
            if (!current.Children.TryGetValue(segment, out MutableModuleExportNode? child))
            {
                child = new MutableModuleExportNode(segment);
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

    private static string GetNamespaceConflictPath(MutableModuleExportNode node, string prefix)
    {
        if (node.Member is not null)
            return node.Member.LuauName;

        foreach (KeyValuePair<string, MutableModuleExportNode> child in node.Children)
            return GetNamespaceConflictPath(child.Value, prefix + "." + child.Key);

        return prefix;
    }

    private static ValidatedModuleExportNode Freeze(MutableModuleExportNode node)
    {
        return new ValidatedModuleExportNode(
            node.Name,
            node.Member,
            node.Children.Values.Select(Freeze).ToImmutableEquatableArray()
        );
    }

    private sealed class MutableModuleExportNode(string name)
    {
        public string Name { get; } = name;

        public NormalizedExportMember? Member { get; set; }

        public Dictionary<string, MutableModuleExportNode> Children { get; } = new(StringComparer.Ordinal);
    }
}
