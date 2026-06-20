using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator.GeneratedExports;

internal sealed class GeneratedExportsModelBuilder
{
    private readonly GeneratedExportsCompilationContext _context;

    private GeneratedExportsModelBuilder(GeneratedExportsCompilationContext context)
    {
        _context = context;
    }

    public static GeneratedExportsModelBuilder? Create(Compilation compilation)
    {
        var context = GeneratedExportsCompilationContext.Create(compilation);
        return context is null ? null : new GeneratedExportsModelBuilder(context);
    }

    public GeneratedExportsTypeAnalysis AnalyzeType(INamedTypeSymbol type)
    {
        var diagnostics = new List<Diagnostic>();
        DiscoveredExportType? discoveredType = GeneratedExportsDiscovery.DiscoverType(type, _context, diagnostics);
        if (discoveredType is null)
            return new GeneratedExportsTypeAnalysis(null, diagnostics.ToImmutableArray(), CanEmitSource: false);

        // Type-level errors are fatal for the generated registration surface. Member-level errors below are
        // recoverable and only remove the invalid member from the generated library tree.
        bool hasFatalTypeErrors = GeneratedExportsValidation.ValidateTypeShape(discoveredType, _context, diagnostics);
        NormalizedExportType normalizedType = GeneratedExportsNormalization.Normalize(
            discoveredType,
            _context,
            diagnostics
        );
        ValidatedExportType validatedType = GeneratedExportsValidation.ValidateMembers(
            discoveredType,
            normalizedType,
            diagnostics
        );
        GeneratedExportSurfaceIr model = GeneratedExportsIrProjector.Project(validatedType);
        return new GeneratedExportsTypeAnalysis(
            model,
            diagnostics.ToImmutableArray(),
            CanEmitSource: !hasFatalTypeErrors
        );
    }
}
