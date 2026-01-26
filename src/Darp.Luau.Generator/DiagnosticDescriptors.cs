using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnsupportedTypeDescriptor = new(
        id: "DLUAU0002",
        title: "Unsupported type for Luau interop",
        messageFormat: "Type '{0}' is not supported for Luau parameter/return marshalling.",
        category: "Darp.Luau.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
