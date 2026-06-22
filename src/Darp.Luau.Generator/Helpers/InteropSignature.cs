namespace Darp.Luau.Generator.Helpers;

internal readonly record struct InteropSignature(
    ImmutableEquatableArray<InteropType> Parameters,
    ImmutableEquatableArray<InteropType> ReturnTypes
);
