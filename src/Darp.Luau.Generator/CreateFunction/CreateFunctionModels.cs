using System.Collections.Immutable;
using Darp.Luau.Generator.Helpers;

namespace Darp.Luau.Generator.CreateFunction;

internal readonly record struct InterceptorLocationData(int Version, string Data);

internal sealed record CreateFunctionModel(InterceptorLocationData Location, InteropSignature Signature);

internal sealed record CreateFunctionAnalysisResult(
    CreateFunctionModel? Model,
    ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics
);
