namespace Darp.Luau.Generator.Helpers;

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
