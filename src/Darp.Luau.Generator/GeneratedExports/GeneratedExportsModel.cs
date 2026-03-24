using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal enum LuauExportedTypeKind
{
    Library,
    Userdata,
}

internal enum LuauExportedMemberKind
{
    Property,
    Method,
}

internal enum LuauExportPropertyAccess
{
    Auto = 0,
    ReadOnly = 1,
    WriteOnly = 2,
    ReadWrite = 3,
}

internal sealed record LuauExportedTypeModel(
    INamedTypeSymbol Symbol,
    LuauExportedTypeKind Kind,
    string? LibraryName,
    ImmutableArray<LuauExportedMemberModel> Members,
    LuauLibraryExportNode? LibraryRoot
);

internal sealed record LuauExportedMemberModel(
    ISymbol Symbol,
    LuauExportedMemberKind Kind,
    string Name,
    ImmutableArray<string> PathSegments,
    LuauExportPropertyAccess Access,
    LuauTypeMapping? PropertyType,
    ImmutableArray<LuauTypeMapping> Parameters,
    ImmutableArray<LuauTypeMapping> ReturnTypes
);

internal sealed class LuauLibraryExportNode(string name)
{
    public string Name { get; } = name;

    public LuauExportedMemberModel? Member { get; set; }

    public Dictionary<string, LuauLibraryExportNode> Children { get; } = new(StringComparer.Ordinal);
}

internal sealed record GeneratedExportsTypeAnalysis(
    LuauExportedTypeModel? Model,
    ImmutableArray<Diagnostic> Diagnostics
);
