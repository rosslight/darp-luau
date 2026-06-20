namespace Darp.Luau.Generator;

internal readonly record struct LuauTypeMapping(
    LuauValueType Type,
    bool IsNullable,
    string? OriginalTypeName,
    bool IsGeneratedUserdata = false
);
