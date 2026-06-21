using Darp.Luau.Generator.Helpers;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class ExportValidator
{
    public static bool ValidateTypeShape(
        DiscoveredExportType discoveredType,
        LuauApiSymbols context,
        List<Diagnostic> diagnostics
    )
    {
        return ValidateType(discoveredType, context, diagnostics);
    }

    public static ValidatedExportType ValidateMembers(
        DiscoveredExportType discoveredType,
        NormalizedExportType normalizedType,
        List<Diagnostic> diagnostics
    )
    {
        ReportDuplicateNames(discoveredType.Symbol, normalizedType.Kind, normalizedType.Members, diagnostics);

        ValidatedLibraryExportNode? libraryRoot =
            normalizedType.Kind == LuauExportedTypeKind.Library
                ? ExportTreeBuilder.BuildLibraryTree(normalizedType, diagnostics)
                : null;
        return new ValidatedExportType(normalizedType, libraryRoot);
    }

    private static bool ValidateType(
        DiscoveredExportType discoveredType,
        LuauApiSymbols context,
        List<Diagnostic> diagnostics
    )
    {
        bool hasFatalTypeErrors = false;
        if (!SymbolExtensions.IsPartial(discoveredType.Symbol))
        {
            hasFatalTypeErrors = true;
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
            string? libraryName = AttributeReader.GetStringConstructorArgument(
                discoveredType.Attribute
            );
            if (string.IsNullOrWhiteSpace(libraryName))
            {
                hasFatalTypeErrors = true;
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                        discoveredType.Origin.Location,
                        "root libraries must supply a non-empty name via [LuauLibrary(\"...\")]"
                    )
                );
            }

            if (discoveredType.Symbol.ContainingType is not null)
            {
                hasFatalTypeErrors = true;
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
                hasFatalTypeErrors = true;
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
                hasFatalTypeErrors = true;
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
                hasFatalTypeErrors = true;
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InstanceLibraryStructNotSupportedDescriptor,
                        discoveredType.Origin.Location
                    )
                );
            }

            return hasFatalTypeErrors;
        }

        if (discoveredType.Symbol.TypeKind != TypeKind.Class)
        {
            hasFatalTypeErrors = true;
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
            hasFatalTypeErrors = true;
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedUserdataManualInteropConflictDescriptor,
                    discoveredType.Origin.Location,
                    discoveredType.Symbol.Name
                )
            );
        }

        return hasFatalTypeErrors;
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
                        SymbolExtensions.GetSymbolLocation(member.Symbol),
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

}
