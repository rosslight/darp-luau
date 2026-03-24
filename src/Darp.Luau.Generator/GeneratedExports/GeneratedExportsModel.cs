using System.Collections.Immutable;
using Darp.Luau.Generator.Helpers;
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

internal sealed record SourceOrigin(string DisplayName, Location Location);

internal sealed record DiscoveredExportType(
    INamedTypeSymbol Symbol,
    LuauExportedTypeKind Kind,
    AttributeData Attribute,
    SourceOrigin Origin,
    ImmutableEquatableArray<DiscoveredExportMember> Members
);

internal abstract record DiscoveredExportMember(string ManagedName, AttributeData Attribute, SourceOrigin Origin);

internal sealed record DiscoveredExportProperty(
    IPropertySymbol Symbol,
    AttributeData Attribute,
    SourceOrigin Origin
) : DiscoveredExportMember(Symbol.Name, Attribute, Origin);

internal sealed record DiscoveredExportMethod(
    IMethodSymbol Symbol,
    AttributeData Attribute,
    SourceOrigin Origin
) : DiscoveredExportMember(Symbol.Name, Attribute, Origin);

internal sealed record NormalizedExportType(
    INamedTypeSymbol Symbol,
    LuauExportedTypeKind Kind,
    string? LibraryName,
    SourceOrigin Origin,
    ImmutableEquatableArray<NormalizedExportMember> Members
);

internal abstract record NormalizedExportMember(
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments,
    SourceOrigin Origin
)
{
    public abstract ISymbol Symbol { get; }
}

internal sealed record NormalizedExportPropertyMember(
    IPropertySymbol PropertySymbol,
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments,
    SourceOrigin Origin,
    NormalizedPropertyContract Property
) : NormalizedExportMember(ManagedName, LuauName, PathSegments, Origin)
{
    public override ISymbol Symbol => PropertySymbol;
}

internal sealed record NormalizedExportMethodMember(
    IMethodSymbol MethodSymbol,
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments,
    SourceOrigin Origin,
    NormalizedMethodContract Method
) : NormalizedExportMember(ManagedName, LuauName, PathSegments, Origin)
{
    public override ISymbol Symbol => MethodSymbol;
}

internal sealed record NormalizedPropertyContract(
    NormalizedPropertyAccessor? Getter,
    NormalizedPropertyAccessor? Setter
);

internal sealed record NormalizedPropertyAccessor(LuauTypeMapping Type);

internal sealed record NormalizedMethodContract(
    ImmutableEquatableArray<LuauTypeMapping> Parameters,
    ImmutableEquatableArray<LuauTypeMapping> ReturnTypes
);

internal sealed record ValidatedExportType(
    NormalizedExportType Type,
    ValidatedLibraryExportNode? LibraryRoot
);

internal sealed record ValidatedLibraryExportNode(
    string Name,
    NormalizedExportMember? Member,
    ImmutableEquatableArray<ValidatedLibraryExportNode> Children
);

internal sealed record GeneratedExportSurfaceIr(
    string ManagedTypeName,
    LuauExportedTypeKind Kind,
    string? LibraryName,
    ImmutableEquatableArray<GeneratedExportMemberIr> Members,
    GeneratedLibraryExportNodeIr? LibraryRoot
);

internal abstract record GeneratedExportMemberIr(
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments
);

internal sealed record GeneratedExportPropertyIr(
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments,
    GeneratedExportAccessorIr? Getter,
    GeneratedExportAccessorIr? Setter
) : GeneratedExportMemberIr(ManagedName, LuauName, PathSegments);

internal sealed record GeneratedExportMethodIr(
    string ManagedName,
    string LuauName,
    ImmutableEquatableArray<string> PathSegments,
    ImmutableEquatableArray<LuauTypeMapping> Parameters,
    ImmutableEquatableArray<LuauTypeMapping> ReturnTypes
) : GeneratedExportMemberIr(ManagedName, LuauName, PathSegments);

internal sealed record GeneratedExportAccessorIr(LuauTypeMapping Type);

internal sealed record GeneratedLibraryExportNodeIr(
    string Name,
    GeneratedExportMemberIr? Member,
    ImmutableEquatableArray<GeneratedLibraryExportNodeIr> Children
);

internal sealed record GeneratedExportsTypeAnalysis(
    GeneratedExportSurfaceIr? Model,
    ImmutableArray<Diagnostic> Diagnostics
);
