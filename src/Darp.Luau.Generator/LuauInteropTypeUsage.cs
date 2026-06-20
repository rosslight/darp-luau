namespace Darp.Luau.Generator;

internal enum LuauInteropTypeUsage
{
    CreateFunctionParameter,
    CreateFunctionReturn,
    LibraryFunctionParameter,
    LibraryFunctionReturn,
    LibraryProperty,
    UserdataPropertyGet,
    UserdataPropertySet,
    UserdataMethodParameter,
    UserdataMethodReturn,
    TypeFile,
}
