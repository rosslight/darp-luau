using Microsoft.CodeAnalysis;

namespace Darp.Luau.Generator;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnsupportedTypeDescriptor = new(
        id: "DLUAU0002",
        title: "Unsupported type for Luau interop",
        messageFormat: "Type '{0}' used for {1} is not supported for Luau parameter/return marshalling",
        category: "Darp.Luau.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidDelegateTypeDescriptor = new(
        id: "DLUAU0003",
        title: "Invalid delegate type",
        messageFormat: "Type '{0}' cannot be used as a delegate type. Expected a concrete delegate type (e.g., Action, Func<string>, or a custom delegate).",
        category: "Darp.Luau.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
