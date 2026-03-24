using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal static class GeneratedExportsPathParser
{
    public static bool TryParseLibraryPath(
        string rawPath,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<string> segments
    )
    {
        return TryParsePath(rawPath, allowDotted: true, location, diagnostics, out segments);
    }

    public static bool TryParseUserdataPath(
        string rawPath,
        string memberName,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<string> segments
    )
    {
        if (rawPath.IndexOf('.') >= 0)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"userdata member '{memberName}' must use a single-segment Luau name"
                )
            );
            segments = [];
            return false;
        }

        return TryParsePath(rawPath, allowDotted: false, location, diagnostics, out segments);
    }

    private static bool TryParsePath(
        string rawPath,
        bool allowDotted,
        Location location,
        List<Diagnostic> diagnostics,
        out ImmutableArray<string> segments
    )
    {
        if (!allowDotted && rawPath.IndexOf('.') >= 0)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGeneratedExportShapeDescriptor,
                    location,
                    $"Luau member name '{rawPath}' must be a single segment"
                )
            );
            segments = [];
            return false;
        }

        string[] splitSegments = rawPath.Split('.');
        if (splitSegments.Length == 0 || splitSegments.Any(static x => x.Length == 0))
        {
            diagnostics.Add(
                Diagnostic.Create(DiagnosticDescriptors.InvalidLuauExportPathDescriptor, location, rawPath)
            );
            segments = [];
            return false;
        }

        segments = splitSegments.ToImmutableArray();
        return true;
    }
}
