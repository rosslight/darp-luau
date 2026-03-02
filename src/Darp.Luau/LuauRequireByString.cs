using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

// [assembly: DisableRuntimeMarshalling]

namespace Darp.Luau;

public static unsafe class LuauRequireByString
{
    public static void EnableRequireByString(this LuauState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        luaopen_require(state.L, &InitRequireConfig, null);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void InitRequireConfig(luarequire_Configuration* config)
    {
        config->is_require_allowed = &IsRequireAllowed;
        config->reset = &Reset;
        config->jump_to_alias = &JumpToAlias;
        config->to_parent = &ToParent;
        config->to_child = &ToChild;
        config->is_module_present = &IsModulePresent;
        config->get_chunkname = &GetChunkname;
        config->get_loadname = &GetLoadname;
        config->get_cache_key = &GetCacheKey;
        config->get_config_status = &GetConfigStatus;
        config->get_config = &GetConfig;
        config->load = &Load;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsRequireAllowed(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        //TODO
        return false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult Reset(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        //TODO
        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        //TODO
        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        //TODO
        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        //TODO
        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        //TODO
        return false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetChunkname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        //TODO
        *sizeOut = 0;
        return luarequire_WriteResult.WRITE_FAILURE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetLoadname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        //TODO
        *sizeOut = 0;
        return luarequire_WriteResult.WRITE_FAILURE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetCacheKey(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        //TODO
        *sizeOut = 0;
        return luarequire_WriteResult.WRITE_FAILURE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        //TODO
        return luarequire_ConfigStatus.CONFIG_ABSENT;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetConfig(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        //TODO
        *sizeOut = 0;
        return luarequire_WriteResult.WRITE_FAILURE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        //TODO
        return 0;
    }
}