using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

//TODO Find a more central location within project!?
[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau;

public static unsafe class LuauRequireByString
{
    private static readonly Navigator s_navigator = new();

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

    private const char ChunkNamePrefix = '@';
    private const string ChunkNameStdIn = "=stdin";

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsRequireAllowed(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        string strChunkName = new((sbyte*)requirerChunkname);
        return Equals(strChunkName, ChunkNameStdIn) || (strChunkName.Length > 0 && strChunkName[0] == ChunkNamePrefix);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult Reset(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        string strChunkName = new((sbyte*)requirerChunkname);
        if (Equals(strChunkName, ChunkNameStdIn))
            return s_navigator.ResetToStdIn();

        if (strChunkName.Length > 0 && strChunkName[0] == ChunkNamePrefix)
            return s_navigator.ResetToPath(strChunkName[1..]);

        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        string strPath = new((sbyte*)path);
        if (!Path.IsPathRooted(strPath))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        return s_navigator.ResetToPath(strPath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        return s_navigator.ToParent();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        string strName = new((sbyte*)name);
        return s_navigator.ToChild(strName);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        return File.Exists(s_navigator.GetFilePath());
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetChunkname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strChunkName = "@" + s_navigator.GetFilePath();
        return Write(strChunkName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetLoadname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strLoadName = s_navigator.GetAbsoluteFilePath();
        return Write(strLoadName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetCacheKey(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strCacheKey = s_navigator.GetAbsoluteFilePath();
        return Write(strCacheKey, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        return s_navigator.GetConfigStatus();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetConfig(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string? strConfig = s_navigator.GetConfig();
        return Write(strConfig, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        //TODO
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);
        return 0;
    }

    private static luarequire_WriteResult Write(string? strContent, byte* buffer, nuint nBufferSize, nuint* nSizeOut)
    {
        if (strContent is null)
            return luarequire_WriteResult.WRITE_FAILURE;

        byte[] bufferContent = Encoding.UTF8.GetBytes(strContent);
        int nBufferSizeContent = bufferContent.Length;

        int nBufferSizeNullTerminated = nBufferSizeContent + 1;
        if (nBufferSize < (nuint)nBufferSizeNullTerminated)
        {
            *nSizeOut = (nuint)nBufferSizeNullTerminated;
            return luarequire_WriteResult.WRITE_BUFFER_TOO_SMALL;
        }

        *nSizeOut = (nuint)nBufferSizeNullTerminated;
        Marshal.Copy(bufferContent, 0, (IntPtr)buffer, nBufferSizeContent);
        *(buffer + nBufferSizeContent) = 0;
        return luarequire_WriteResult.WRITE_SUCCESS;
    }

    private class Navigator
    {
        public luarequire_NavigateResult ResetToStdIn()
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public luarequire_NavigateResult ResetToPath(string strPath)
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public luarequire_NavigateResult ToParent()
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public luarequire_NavigateResult ToChild(string strName)
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public string GetFilePath()
        {
            //TODO
            return "";
        }

        public string GetAbsoluteFilePath()
        {
            //TODO
            return "";
        }

        public luarequire_ConfigStatus GetConfigStatus()
        {
            //TODO
            return luarequire_ConfigStatus.CONFIG_ABSENT;
        }

        public string? GetConfig()
        {
            //TODO
            return null;
        }
    }
}

