namespace Darp.Luau.Generator.Helpers;

internal enum LuauInteropTypeUsage
{
    CreateFunctionParameter,
    CreateFunctionReturn,
    ModuleFunctionParameter,
    ModuleFunctionReturn,
    ModuleProperty,
    UserdataPropertyGet,
    UserdataPropertySet,
    UserdataMethodParameter,
    UserdataMethodReturn,
    TypeFile,
}
